using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Application.Features.Users.LoginVerify;
using noKeyCloud.Domain.Entities;
using noKeyCloud.Domain.Enums;
using noKeyCloud.Domain.ValueObjects;

namespace noKeyCloud.Application.UnitTests.Features.Users.Login;

public class LoginVerifyTests
{
    private const string ServerLoginState = "server-login-state";
    private const string FinishLoginRequest = "finish-login-request";
    private const string SessionKey = "session-key";
    private const string JwtToken = "jwt-token";
    private const string RefreshToken = "refresh-token";

    private static readonly byte[] WrappedMasterKey = [10, 20, 30];
    private static readonly byte[] KeySalt = [.. "(2<"u8];
    private static readonly Argon2idParams KdfParams = Argon2idParams.Default;

    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IMasterKeyWrapRepository> _masterKeyWrapRepositoryMock;
    private readonly LoginVerifyCommandHandler _handler;

    private static readonly JwtOptions TestJwtOptions = new()
    {
        SecretKey = "unused-in-tests",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeHours = 24
    };

    public LoginVerifyTests()
    {
        _authServiceMock = new Mock<IAuthService> { DefaultValue = DefaultValue.Mock };
        _folderRepositoryMock = new Mock<IFolderRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _masterKeyWrapRepositoryMock = new Mock<IMasterKeyWrapRepository>();

        _handler = new LoginVerifyCommandHandler(
            _authServiceMock.Object,
            _folderRepositoryMock.Object,
            _userRepositoryMock.Object,
            _masterKeyWrapRepositoryMock.Object,
            Options.Create(TestJwtOptions));
    }

    private static User CreateFakeUser(Guid id, bool isAdmin = false) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testuser",
            isAdmin: isAdmin);

    private static MasterKeyWrap CreateFakeOpaqueMasterKeyWrap(Guid userId) =>
        new(
            id: Guid.NewGuid(),
            userId: userId,
            type: MasterKeyWrapType.OpaquePassword,
            wrappedMasterKey: WrappedMasterKey,
            salt: KeySalt,
            argon2idParams: KdfParams);

    private static Folder CreateFakeFolder(Guid userId, Guid folderId)
    {
        var now = DateTime.UtcNow;
        return new Folder(
            id: folderId,
            encryptedName: [1],
            encryptedKey: [2],
            createdAt: now,
            updatedAt: now,
            parentFolderId: null,
            userId: userId);
    }

    private void SetupHappyPath(Guid sessionId, Guid userId, bool isAdmin = false)
    {
        var user = CreateFakeUser(userId, isAdmin);
        var folder = CreateFakeFolder(userId, Guid.NewGuid());
        var masterKeyWrap = CreateFakeOpaqueMasterKeyWrap(userId);

        _authServiceMock
            .Setup(s => s.OpaqueSessions.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync(ServerLoginState);
        _authServiceMock
            .Setup(o => o.Opaque.ServerFinishLogin(ServerLoginState, FinishLoginRequest))
            .Returns(SessionKey);
        _authServiceMock
            .Setup(s => s.OpaqueSessions.GetUserIdAsync(sessionId))
            .ReturnsAsync(userId);
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _masterKeyWrapRepositoryMock
            .Setup(r => r.GetByUserIdAndTypeAsync(userId, MasterKeyWrapType.OpaquePassword,It.IsAny<CancellationToken>()))
            .ReturnsAsync(masterKeyWrap);
        _authServiceMock
            .Setup(j => j.Jwt.JwtTokenService(userId, isAdmin, It.IsAny<Guid>()))
            .ReturnsAsync(JwtToken);
        _authServiceMock
            .Setup(r => r.RefreshTokens.GenerateRefreshToken())
            .Returns(RefreshToken);
        _authServiceMock
            .Setup(r => r.RefreshTokens.StoreRefreshTokenAsync(
                userId, It.IsAny<Guid>(), RefreshToken, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(f => f.GetUserHomeFolder(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _authServiceMock
            .Setup(s => s.OpaqueSessions.DeleteSessionAsync(sessionId))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_WhenSessionIdIsNotGuid_ShouldReturnFailure()
    {
        var command = new LoginVerifyCommand("not-a-guid", FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid session ID format.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ShouldReturnFailureAndSkipOpaque()
    {
        var sessionId = Guid.NewGuid();
        _authServiceMock
            .Setup(s => s.OpaqueSessions.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync((string?)null);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldReturnJwtAndRefreshToken()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(JwtToken, result.Value.JwtToken);
        Assert.Equal(RefreshToken, result.Value.RefreshToken);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldGenerateUniqueSessionIdAndUseItForJwtAndStore()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        Guid? jwtSessionId = null;
        Guid? storeSessionId = null;

        _authServiceMock
            .Setup(j => j.Jwt.JwtTokenService(userId, false, It.IsAny<Guid>()))
            .Callback<Guid?, bool, Guid>((_, _, sid) => jwtSessionId = sid)
            .ReturnsAsync(JwtToken);
        _authServiceMock
            .Setup(r => r.RefreshTokens.StoreRefreshTokenAsync(userId, It.IsAny<Guid>(), RefreshToken, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, TimeSpan, CancellationToken>((_, sid, _, _, _) => storeSessionId = sid)
            .Returns(Task.CompletedTask);

        await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.NotNull(jwtSessionId);
        Assert.NotNull(storeSessionId);
        Assert.NotEqual(Guid.Empty, jwtSessionId!.Value);
        Assert.Equal(jwtSessionId, storeSessionId);
        Assert.NotEqual(sessionId, jwtSessionId.Value);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldStoreRefreshTokenWith24HourExpiry()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        _authServiceMock.Verify(
            r => r.RefreshTokens.StoreRefreshTokenAsync(
                userId, It.IsAny<Guid>(), RefreshToken, TimeSpan.FromHours(24), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldDeleteSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        _authServiceMock.Verify(s => s.OpaqueSessions.DeleteSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeleteSessionFails_ShouldReturnFailure()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);
        _authServiceMock
            .Setup(s => s.OpaqueSessions.DeleteSessionAsync(sessionId))
            .ReturnsAsync(false);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not remove session", result.Error);
    }

    [Fact]
    public async Task Handle_WhenOpaqueFinishLoginThrows_ShouldReturnFailureAndCleanupSession()
    {
        var sessionId = Guid.NewGuid();
        _authServiceMock
            .Setup(s => s.OpaqueSessions.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync(ServerLoginState);
        _authServiceMock
            .Setup(o => o.Opaque.ServerFinishLogin(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Opaque verification failed"));
        _authServiceMock
            .Setup(s => s.OpaqueSessions.DeleteSessionAsync(sessionId))
            .ReturnsAsync(true);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Login verification failed", result.Error);
        _authServiceMock.Verify(s => s.OpaqueSessions.DeleteSessionAsync(sessionId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldReturnEncryptedMasterKeySaltAndArgon2Params()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WrappedMasterKey, result.Value!.ResponsePayload.EncryptedMasterKey);
        Assert.Equal(KeySalt, result.Value.ResponsePayload.KeySalt);
    }

    [Fact]
    public async Task Handle_WhenOpaqueMasterKeyWrapMissing_ShouldReturnFailure()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);
        _masterKeyWrapRepositoryMock
            .Setup(r => r.GetByUserIdAndTypeAsync(userId, MasterKeyWrapType.OpaquePassword,It.IsAny<CancellationToken>()))
            .ReturnsAsync((MasterKeyWrap?)null);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_ShouldRequestAdminJwt()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId, isAdmin: true);

        await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        _authServiceMock.Verify(j => j.Jwt.JwtTokenService(userId, true, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIdMissingFromSession_ShouldReturnFailure()
    {
        var sessionId = Guid.NewGuid();
        _authServiceMock.Setup(s => s.OpaqueSessions.GetServerLoginStateAsync(sessionId)).ReturnsAsync(ServerLoginState);
        _authServiceMock.Setup(o => o.Opaque.ServerFinishLogin(ServerLoginState, FinishLoginRequest)).Returns(SessionKey);
        _authServiceMock.Setup(s => s.OpaqueSessions.GetUserIdAsync(sessionId)).ReturnsAsync((Guid?)null);

        var result = await _handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not retrieve user associated with the session.", result.Error);
    }
}