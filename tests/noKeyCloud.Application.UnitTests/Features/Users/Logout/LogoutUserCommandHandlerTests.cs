using Moq;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Users.Logout;
using noKeyCloud.Application.Security.Models;

namespace noKeyCloud.Application.UnitTests.Features.Users.Logout;

public class LogoutUserCommandHandlerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly LogoutUserCommandHandler _handler;

    public LogoutUserCommandHandlerTests()
    {
        _authServiceMock = new Mock<IAuthService>{DefaultValue = DefaultValue.Mock};
        _handler = new LogoutUserCommandHandler(
            _authServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_WithJtiAndTtl_RevokesOnlyCurrentSessionAndDenylistsJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-refresh-token";
        const string jti = "jwt-id-123";
        var remainingTtl = TimeSpan.FromMinutes(10);

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, jti, remainingTtl),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("User logged out successfully.", result.Value.Message);

        _authServiceMock.Verify(
            x => x.RefreshTokens.RevokeSessionAsync(userId, sessionId, It.IsAny<CancellationToken>()),
            Times.Once);

        _authServiceMock.Verify(
            x => x.RefreshTokens.RevokeAllSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _authServiceMock.Verify(
            x => x.Jwt.RevokeAsync(jti, remainingTtl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidToken_WithoutJti_DoesNotDenylistJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-token";

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, Jti: null, RemainingTtl: TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _authServiceMock.Verify(
            x => x.RefreshTokens.RevokeSessionAsync(userId, sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _authServiceMock.Verify(
            x => x.Jwt.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ValidToken_WithBlankJti_DoesNotDenylistJwt(string blankJti)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-token";

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, blankJti, TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _authServiceMock.Verify(
            x => x.Jwt.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidToken_WithZeroTtl_DoesNotDenylistJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-token";

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, "jti", RemainingTtl: TimeSpan.Zero),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _authServiceMock.Verify(
            x => x.Jwt.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsFailureAndDoesNothingElse()
    {
        var userId = Guid.NewGuid();

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenInfo?)null);

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, "unknown", "jti", TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Token.NotFound", result.Error);

        _authServiceMock.Verify(
            x => x.RefreshTokens.RevokeSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _authServiceMock.Verify(
            x => x.Jwt.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TokenOwnedByDifferentUser_ReturnsFailureAndDoesNotRevoke()
    {
        var requestUserId = Guid.NewGuid();
        var actualOwnerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "foreign-token";

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(actualOwnerId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(requestUserId, token, "jti", TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Token.NotFound", result.Error);

        _authServiceMock.Verify(
            x => x.RefreshTokens.RevokeSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _authServiceMock.Verify(
            x => x.Jwt.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldForwardCancellationTokenToAllCollaborators()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "tok";
        const string jti = "jti";
        var ttl = TimeSpan.FromMinutes(5);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _authServiceMock
            .Setup(x => x.RefreshTokens.FindByTokenAsync(token, ct))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        await _handler.Handle(
            new LogoutUserCommand(userId, token, jti, ttl),
            cts.Token);

        _authServiceMock.Verify(x => x.RefreshTokens.FindByTokenAsync(token, ct), Times.Once);
        _authServiceMock.Verify(x => x.RefreshTokens.RevokeSessionAsync(userId, sessionId, ct), Times.Once);
        _authServiceMock.Verify(x => x.Jwt.RevokeAsync(jti, ttl, ct), Times.Once);
    }
}