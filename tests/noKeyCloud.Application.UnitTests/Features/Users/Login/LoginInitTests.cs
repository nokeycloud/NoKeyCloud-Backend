using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Users.LoginInit;
using noKeyCloud.Application.Security.Models;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users.Login;

public class LoginInitTests
{
    private const string ServerSetup = "server-setup";
    private const string StartLoginRequest = "client-start-login-request";
    private const string ServerLoginState = "server-login-state";
    private const string LoginResponse = "server-login-response";

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOpaqueServerSetupProvider> _serverSetupProviderMock;
    private readonly Mock<IOpaqueAuthService> _opaqueAuthServiceMock;
    private readonly Mock<IOpaqueSessionStore> _sessionStoreMock;
    private readonly LoginInitCommandHandler _handler;

    public LoginInitTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _serverSetupProviderMock = new Mock<IOpaqueServerSetupProvider>();
        _opaqueAuthServiceMock = new Mock<IOpaqueAuthService>();
        _sessionStoreMock = new Mock<IOpaqueSessionStore>();

        _handler = new LoginInitCommandHandler(
            _userRepositoryMock.Object,
            _serverSetupProviderMock.Object,
            _opaqueAuthServiceMock.Object,
            _sessionStoreMock.Object);
    }

    private static User CreateFakeUser(Guid id) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testuser",
            registrationRecord: "registration-record",
            encryptedMasterKey: [1],
            keySalt: [2]);

    private void SetupOpaqueSuccess(User user)
    {
        _serverSetupProviderMock
            .Setup(p => p.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServerSetup);

        _opaqueAuthServiceMock
            .Setup(s => s.ServerStartLogin(
                ServerSetup,
                StartLoginRequest,
                user.UserIdentifier.ToString(),
                user.RegistrationRecord))
            .Returns(new OpaqueServerStartLoginResult(ServerLoginState, LoginResponse));
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnInvalidCredentialsAndSkipOpaque()
    {
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new LoginInitCommand("missing-user", "missing@email.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid credentials.", result.Error);
        Assert.Null(result.Value);
        _serverSetupProviderMock.Verify(
            p => p.GetServerSetupAsync(It.IsAny<CancellationToken>()), Times.Never);
        _opaqueAuthServiceMock.Verify(
            s => s.ServerStartLogin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUsernameProvided_ShouldLookupUserByUsername()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        SetupOpaqueSuccess(user);

        var command = new LoginInitCommand("testuser", "ignored@email.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepositoryMock.Verify(
            r => r.GetUserByUsernameOrEmailAsync("testuser", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUsernameIsEmpty_ShouldFallbackToEmailForLookup()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync("email@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        SetupOpaqueSuccess(user);

        var command = new LoginInitCommand(string.Empty, "email@test.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepositoryMock.Verify(
            r => r.GetUserByUsernameOrEmailAsync("email@test.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUsernameIsWhitespace_ShouldFallbackToEmailForLookup()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync("email@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        SetupOpaqueSuccess(user);

        var command = new LoginInitCommand("   ", "email@test.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepositoryMock.Verify(
            r => r.GetUserByUsernameOrEmailAsync("email@test.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOpaqueSucceeds_ShouldReturnLoginResponseAndNonEmptySessionId()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupOpaqueSuccess(user);

        var command = new LoginInitCommand("testuser", "test@email.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(LoginResponse, result.Value.LoginResponse);
        Assert.NotEqual(Guid.Empty, result.Value.SessionId);
    }

    [Fact]
    public async Task Handle_WhenOpaqueSucceeds_ShouldSaveSessionWithUserAndState()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupOpaqueSuccess(user);

        var command = new LoginInitCommand("testuser", "test@email.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        _sessionStoreMock.Verify(
            s => s.SaveSessionAsync(
                result.Value!.SessionId,
                user.Id,
                ServerLoginState,
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOpaqueSucceeds_ShouldPassUserIdentifierAndRegistrationRecordToOpaqueService()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupOpaqueSuccess(user);

        var command = new LoginInitCommand("testuser", "test@email.com", StartLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _opaqueAuthServiceMock.Verify(
            s => s.ServerStartLogin(
                ServerSetup,
                StartLoginRequest,
                user.UserIdentifier.ToString(),
                user.RegistrationRecord),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOpaqueThrows_ShouldReturnInvalidClientLoginRequest()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _serverSetupProviderMock
            .Setup(p => p.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServerSetup);
        _opaqueAuthServiceMock
            .Setup(s => s.ServerStartLogin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Opaque failure"));

        var command = new LoginInitCommand("testuser", "test@email.com", StartLoginRequest);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid client login request", result.Error);
        _sessionStoreMock.Verify(
            s => s.SaveSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenServerSetupProviderThrows_ShouldPropagateException()
    {
        var user = CreateFakeUser(Guid.NewGuid());
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _serverSetupProviderMock
            .Setup(p => p.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Setup missing"));

        var command = new LoginInitCommand("testuser", "test@email.com", StartLoginRequest);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldNotCallServerSetupProvider()
    {
        _userRepositoryMock
            .Setup(r => r.GetUserByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new LoginInitCommand("ghost", "ghost@email.com", StartLoginRequest);

        await _handler.Handle(command, CancellationToken.None);

        _serverSetupProviderMock.Verify(
            p => p.GetServerSetupAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}