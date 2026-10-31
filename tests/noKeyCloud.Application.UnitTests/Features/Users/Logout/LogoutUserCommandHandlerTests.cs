using Moq;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Users.Logout;

namespace noKeyCloud.Application.UnitTests.Features.Users.Logout;

public class LogoutUserCommandHandlerTests
{
    private readonly Mock<IRefreshTokenProvider> _refreshTokenProviderMock;
    private readonly Mock<IJwtRevocationService> _jwtRevocationServiceMock;
    private readonly LogoutUserCommandHandler _handler;

    public LogoutUserCommandHandlerTests()
    {
        _refreshTokenProviderMock = new Mock<IRefreshTokenProvider>();
        _jwtRevocationServiceMock = new Mock<IJwtRevocationService>();
        _handler = new LogoutUserCommandHandler(
            _refreshTokenProviderMock.Object, 
            _jwtRevocationServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_WithJti_ReturnsSuccessMessageAndRemovesTokenAndRevokesJwt()
    {
        var userId = Guid.NewGuid();
        const string token = "valid-refresh-token";
        const string jti = "jwt-id-123";
        var remainingTtl = TimeSpan.FromMinutes(10);
        
        _refreshTokenProviderMock
            .Setup(x => x.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var command = new LogoutUserCommand(userId, token, jti, remainingTtl);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("User logged out successfully.", result.Value.Message);
        
        _refreshTokenProviderMock.Verify(x => x.InvalidateRefreshTokenAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _jwtRevocationServiceMock.Verify(x => x.RevokeAsync(jti, remainingTtl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_WithoutJti_DoesNotCallRevokeAsync()
    {
        var userId = Guid.NewGuid();
        const string token = "valid-refresh-token";
        
        _refreshTokenProviderMock
            .Setup(x => x.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        
        var command = new LogoutUserCommand(userId, token, null, TimeSpan.Zero);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
        
        _refreshTokenProviderMock.Verify(x => x.InvalidateRefreshTokenAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _jwtRevocationServiceMock.Verify(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidOrRemovedToken_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        const string commandToken = "some-token";
        
        _refreshTokenProviderMock
            .Setup(x => x.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var command = new LogoutUserCommand(userId, commandToken, "jti", TimeSpan.FromMinutes(5));
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("Token.NotFound", result.Error);
        
        _jwtRevocationServiceMock.Verify(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task Handle_MismatchedToken_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        const string commandToken = "some-token-from-client";
        const string cacheToken = "totally-different-token-in-cache";
        
        _refreshTokenProviderMock
            .Setup(x => x.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheToken);

        var command = new LogoutUserCommand(userId, commandToken, "jti", TimeSpan.FromMinutes(5));
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("Token.NotFound", result.Error);
        
        _refreshTokenProviderMock.Verify(x => x.InvalidateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _jwtRevocationServiceMock.Verify(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}