using Moq;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Users.RegisterInit;

namespace noKeyCloud.Application.UnitTests.Features.Users.RegisterInit;

public class RegisterResponseUserHandlerTests
{
    private const string ValidUserIdentifier          = "user@example.com";
    private const string ValidRegistrationRequest     = "b64-opaque-client-registration-request";
    private const string ExpectedServerSetup          = "opaque-server-setup-string";
    private const string ExpectedRegistrationResponse = "b64-opaque-server-registration-response";

    private readonly Mock<IOpaqueAuthService> _opaqueAuthServiceMock;
    private readonly Mock<IOpaqueServerSetupProvider> _serverSetupProviderMock;
    private readonly RegisterResponseUserHandler _handler;

    public RegisterResponseUserHandlerTests()
    {
        _opaqueAuthServiceMock    = new Mock<IOpaqueAuthService>();
        _serverSetupProviderMock  = new Mock<IOpaqueServerSetupProvider>();
        _handler = new RegisterResponseUserHandler(
            _opaqueAuthServiceMock.Object,
            _serverSetupProviderMock.Object);
    }
    
    private void ArrangeHappyPath()
    {
        _serverSetupProviderMock
            .Setup(x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpectedServerSetup);

        _opaqueAuthServiceMock
            .Setup(x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(ExpectedRegistrationResponse);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessResult()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsResponseContainingRegistrationResponseFromAuthService()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);

        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.NotNull(result.Value);
        Assert.Equal(ExpectedRegistrationResponse, result.Value!.RegistrationResponse);
    }

    [Theory]
    [InlineData(null, ValidRegistrationRequest)]
    [InlineData("", ValidRegistrationRequest)]
    [InlineData("   ", ValidRegistrationRequest)]
    [InlineData("\t", ValidRegistrationRequest)]
    [InlineData("\n", ValidRegistrationRequest)]
    [InlineData(ValidUserIdentifier, null)]
    [InlineData(ValidUserIdentifier, "")]
    [InlineData(ValidUserIdentifier, "   ")]
    [InlineData(ValidUserIdentifier, "\t")]
    [InlineData(ValidUserIdentifier, "\n")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task Handle_MissingOrWhitespaceInputs_ReturnsFailureWithExpectedMessage(
        string? userIdentifier,
        string? registrationRequest)
    {
        var command = new RegisterResponseUserCommand(userIdentifier!, registrationRequest!);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("User identifier and registration request are required.", result.Error);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(null, ValidRegistrationRequest)]
    [InlineData("", ValidRegistrationRequest)]
    [InlineData("   ", ValidRegistrationRequest)]
    [InlineData(ValidUserIdentifier, null)]
    [InlineData(ValidUserIdentifier, "")]
    [InlineData(ValidUserIdentifier, "   ")]
    public async Task Handle_InvalidInput_DoesNotCallServerSetupProvider(
        string? userIdentifier,
        string? registrationRequest)
    {
        var command = new RegisterResponseUserCommand(userIdentifier!, registrationRequest!);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _serverSetupProviderMock.Verify(
            x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, ValidRegistrationRequest)]
    [InlineData("", ValidRegistrationRequest)]
    [InlineData("   ", ValidRegistrationRequest)]
    [InlineData(ValidUserIdentifier, null)]
    [InlineData(ValidUserIdentifier, "")]
    [InlineData(ValidUserIdentifier, "   ")]
    public async Task Handle_InvalidInput_DoesNotCallAuthService(
        string? userIdentifier,
        string? registrationRequest)
    {
        var command = new RegisterResponseUserCommand(userIdentifier!, registrationRequest!);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }
    

    [Fact]
    public async Task Handle_ValidRequest_CallsServerSetupProviderExactlyOnce()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _serverSetupProviderMock.Verify(
            x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesCancellationTokenToServerSetupProvider()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        using var cts = new CancellationTokenSource();
        
        await _handler.Handle(command, cts.Token);
        
        _serverSetupProviderMock.Verify(
            x => x.GetServerSetupAsync(cts.Token),
            Times.Once);
    }
    
    [Fact]
    public async Task Handle_ValidRequest_CallsAuthServiceExactlyOnce()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesServerSetupToAuthService()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                ExpectedServerSetup,
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesUserIdentifierToAuthService()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);

        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                ValidUserIdentifier,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesRegistrationRequestToAuthService()
    {
        ArrangeHappyPath();
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                ValidRegistrationRequest),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("some-opaque-response")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.somebase64.payload")]
    public async Task Handle_AuthServiceReturnsValue_MapsItDirectlyIntoResponse(string authServiceReturn)
    {
        _serverSetupProviderMock
            .Setup(x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpectedServerSetup);

        _opaqueAuthServiceMock
            .Setup(x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(authServiceReturn);

        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(authServiceReturn, result.Value!.RegistrationResponse);
    }
    
    [Fact]
    public async Task Handle_ServerSetupProviderThrows_PropagatesException()
    {
        _serverSetupProviderMock
            .Setup(x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to load OPAQUE setup."));

        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("Failed to load OPAQUE setup.", exception.Message);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AuthServiceThrows_PropagatesException()
    {
        _serverSetupProviderMock
            .Setup(x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpectedServerSetup);

        _opaqueAuthServiceMock
            .Setup(x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Throws(new InvalidOperationException("OPAQUE server registration response failed."));

        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("OPAQUE server registration response failed.", exception.Message);
    }

    [Fact]
    public async Task Handle_CancelledToken_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _serverSetupProviderMock
            .Setup(x => x.GetServerSetupAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(ExpectedServerSetup);
            });

        var command = new RegisterResponseUserCommand(ValidUserIdentifier, ValidRegistrationRequest);
        
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.Handle(command, cts.Token));
    }
    
    [Fact]
    public async Task Handle_UserIdentifierWithLeadingTrailingWhitespace_IsPassedThroughUnchanged()
    {
        ArrangeHappyPath();
        const string identifierWithSpaces = "  user@example.com  ";
        var command = new RegisterResponseUserCommand(identifierWithSpaces, ValidRegistrationRequest);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                identifierWithSpaces,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_VeryLongRegistrationRequest_IsPassedThroughUnchanged()
    {
        ArrangeHappyPath();
        var longRequest = new string('A', 10_000);
        var command = new RegisterResponseUserCommand(ValidUserIdentifier, longRequest);
        
        await _handler.Handle(command, CancellationToken.None);
        
        _opaqueAuthServiceMock.Verify(
            x => x.ServerRegisterResponse(
                It.IsAny<string>(),
                It.IsAny<string>(),
                longRequest),
            Times.Once);
    }
}