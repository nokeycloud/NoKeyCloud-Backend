using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Application.Features.Users.LoginVerify;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users.Login;

public class LoginVerifyTests
{
    private const string ServerLoginState = "server-login-state";
    private const string FinishLoginRequest = "finish-login-request";
    private const string SessionKey = "session-key";
    private const string JwtToken = "jwt-token";
    private const string RefreshToken = "refresh-token";

    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IOpaqueSessionStore> _sessionStoreMock;
    private readonly Mock<IRefreshTokenProvider> _refreshTokenProviderMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOpaqueAuthService> _opaqueAuthServiceMock;
    private readonly LoginVerifyCommandHandler _handler;
    
    private static readonly JwtOptions TestJwtOptions = new()
    {
        SecretKey = "unused-in-tests",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeHours = 24
    };

    public LoginVerifyTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _sessionStoreMock = new Mock<IOpaqueSessionStore>();
        _refreshTokenProviderMock = new Mock<IRefreshTokenProvider>();
        _folderRepositoryMock = new Mock<IFolderRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _opaqueAuthServiceMock = new Mock<IOpaqueAuthService>();

        _handler = new LoginVerifyCommandHandler(
            _jwtServiceMock.Object,
            _sessionStoreMock.Object,
            _refreshTokenProviderMock.Object,
            _folderRepositoryMock.Object,
            _userRepositoryMock.Object,
            _opaqueAuthServiceMock.Object,
            Options.Create(TestJwtOptions));
    }

    private static User CreateFakeUser(Guid id, bool isAdmin = false) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testuser",
            registrationRecord: "record",
            encryptedMasterKey: [10, 20, 30],
            keySalt: [.. "(2<"u8],
            isAdmin: isAdmin);

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

        _sessionStoreMock
            .Setup(s => s.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync(ServerLoginState);
        _opaqueAuthServiceMock
            .Setup(o => o.ServerFinishLogin(ServerLoginState, FinishLoginRequest))
            .Returns(SessionKey);
        _sessionStoreMock
            .Setup(s => s.GetUserIdAsync(sessionId))
            .ReturnsAsync(userId);
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtServiceMock
            .Setup(j => j.JwtTokenService(userId, isAdmin))
            .ReturnsAsync(JwtToken);
        _refreshTokenProviderMock
            .Setup(r => r.GenerateRefreshToken())
            .Returns(RefreshToken);
        _refreshTokenProviderMock
            .Setup(r => r.StoreRefreshTokenAsync(userId, RefreshToken, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(f => f.GetUserHomeFolder(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _sessionStoreMock
            .Setup(s => s.DeleteSessionAsync(sessionId))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_WhenSessionIdIsNotGuid_ShouldReturnFailure()
    {
        var command = new LoginVerifyCommand("not-a-guid", FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid session ID format.", result.Error);
        _sessionStoreMock.Verify(
            s => s.GetServerLoginStateAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ShouldReturnFailureAndSkipOpaque()
    {
        var sessionId = Guid.NewGuid();
        _sessionStoreMock
            .Setup(s => s.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync((string?)null);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Session not found.", result.Error);
        _opaqueAuthServiceMock.Verify(
            o => o.ServerFinishLogin(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionStateEmpty_ShouldFallThroughToUserLookupFailure()
    {
        var sessionId = Guid.NewGuid();
        _sessionStoreMock
            .Setup(s => s.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync(string.Empty);
        _sessionStoreMock
            .Setup(s => s.GetUserIdAsync(sessionId))
            .ReturnsAsync((Guid?)null);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not retrieve user associated with the session.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdMissingFromSession_ShouldReturnFailure()
    {
        var sessionId = Guid.NewGuid();

        _sessionStoreMock
            .Setup(s => s.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync(ServerLoginState);
        _opaqueAuthServiceMock
            .Setup(o => o.ServerFinishLogin(ServerLoginState, FinishLoginRequest))
            .Returns(SessionKey);
        _sessionStoreMock
            .Setup(s => s.GetUserIdAsync(sessionId))
            .ReturnsAsync((Guid?)null);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not retrieve user associated with the session.", result.Error);
        _jwtServiceMock.Verify(
            j => j.JwtTokenService(It.IsAny<Guid?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeleteSessionFails_ShouldReturnFailure()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        _sessionStoreMock
            .Setup(s => s.DeleteSessionAsync(sessionId))
            .ReturnsAsync(false);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not remove session", result.Error);
    }

    [Fact]
    public async Task Handle_WhenOpaqueFinishLoginThrows_ShouldReturnFailureAndCleanupSession()
    {
        var sessionId = Guid.NewGuid();

        _sessionStoreMock
            .Setup(s => s.GetServerLoginStateAsync(sessionId))
            .ReturnsAsync(ServerLoginState);
        _opaqueAuthServiceMock
            .Setup(o => o.ServerFinishLogin(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Opaque verification failed"));
        _sessionStoreMock
            .Setup(s => s.DeleteSessionAsync(sessionId))
            .ReturnsAsync(true);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Login verification failed", result.Error);
        _sessionStoreMock.Verify(
            s => s.DeleteSessionAsync(sessionId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldReturnJwtAndRefreshToken()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(JwtToken, result.Value.JwtToken);
        Assert.Equal(RefreshToken, result.Value.RefreshToken);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldReturnEncryptedMasterKeyAndSalt()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(new byte[] { 10, 20, 30 }, result.Value.ResponsePayload.EncryptedMasterKey);
        Assert.Equal("(2<"u8.ToArray(), result.Value.ResponsePayload.KeySalt);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldStoreRefreshTokenWith24HourExpiry()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            r => r.StoreRefreshTokenAsync(
                userId,
                RefreshToken,
                TimeSpan.FromHours(24),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldDeleteSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _sessionStoreMock.Verify(
            s => s.DeleteSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldCallOpaqueFinishLoginWithStateAndRequest()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _opaqueAuthServiceMock.Verify(
            o => o.ServerFinishLogin(ServerLoginState, FinishLoginRequest),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldGenerateRefreshTokenExactlyOnce()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            r => r.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_ShouldRequestAdminJwt()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId, isAdmin: true);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _jwtServiceMock.Verify(
            j => j.JwtTokenService(userId, true), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotAdmin_ShouldRequestNonAdminJwt()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId, isAdmin: false);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _jwtServiceMock.Verify(
            j => j.JwtTokenService(userId, false), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldFetchRootFolderForUser()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        var command = new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _folderRepositoryMock.Verify(
            f => f.GetUserHomeFolder(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task Handle_WhenRefreshTokenLifetimeHoursIsConfigured_ShouldUseConfiguredValue()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupHappyPath(sessionId, userId);

        const int configuredHours = 6;
        var handler = new LoginVerifyCommandHandler(
            _jwtServiceMock.Object,
            _sessionStoreMock.Object,
            _refreshTokenProviderMock.Object,
            _folderRepositoryMock.Object,
            _userRepositoryMock.Object,
            _opaqueAuthServiceMock.Object,
            Options.Create(new JwtOptions
            {
                SecretKey = "unused-in-tests",
                AccessTokenLifetimeMinutes = 15,
                RefreshTokenLifetimeHours = configuredHours
            }));

        await handler.Handle(
            new LoginVerifyCommand(sessionId.ToString(), FinishLoginRequest),
            CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            r => r.StoreRefreshTokenAsync(
                userId,
                RefreshToken,
                TimeSpan.FromHours(configuredHours),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}