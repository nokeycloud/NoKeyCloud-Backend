using Moq;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Users.Logout;
using noKeyCloud.Application.Security.Models;

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
    public async Task Handle_ValidToken_WithJtiAndTtl_RevokesOnlyCurrentSessionAndDenylistsJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-refresh-token";
        const string jti = "jwt-id-123";
        var remainingTtl = TimeSpan.FromMinutes(10);

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, jti, remainingTtl),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("User logged out successfully.", result.Value.Message);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeSessionAsync(userId, sessionId, It.IsAny<CancellationToken>()),
            Times.Once);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeAllSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _jwtRevocationServiceMock.Verify(
            x => x.RevokeAsync(jti, remainingTtl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidToken_WithoutJti_DoesNotDenylistJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-token";

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, Jti: null, RemainingTtl: TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _refreshTokenProviderMock.Verify(
            x => x.RevokeSessionAsync(userId, sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _jwtRevocationServiceMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]       // empty jti
    [InlineData("   ")]    // whitespace jti
    public async Task Handle_ValidToken_WithBlankJti_DoesNotDenylistJwt(string blankJti)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-token";

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, blankJti, TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _jwtRevocationServiceMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidToken_WithZeroTtl_DoesNotDenylistJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "valid-token";

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, token, "jti", RemainingTtl: TimeSpan.Zero),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _jwtRevocationServiceMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsFailureAndDoesNothingElse()
    {
        var userId = Guid.NewGuid();

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenInfo?)null);

        var result = await _handler.Handle(
            new LogoutUserCommand(userId, "unknown", "jti", TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Token.NotFound", result.Error);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jwtRevocationServiceMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TokenOwnedByDifferentUser_ReturnsFailureAndDoesNotRevoke()
    {
        var requestUserId = Guid.NewGuid();
        var actualOwnerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string token = "foreign-token";

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(actualOwnerId, sessionId));

        var result = await _handler.Handle(
            new LogoutUserCommand(requestUserId, token, "jti", TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Token.NotFound", result.Error);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jwtRevocationServiceMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
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

        _refreshTokenProviderMock
            .Setup(x => x.FindByTokenAsync(token, ct))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));

        await _handler.Handle(
            new LogoutUserCommand(userId, token, jti, ttl),
            cts.Token);

        _refreshTokenProviderMock.Verify(x => x.FindByTokenAsync(token, ct), Times.Once);
        _refreshTokenProviderMock.Verify(x => x.RevokeSessionAsync(userId, sessionId, ct), Times.Once);
        _jwtRevocationServiceMock.Verify(x => x.RevokeAsync(jti, ttl, ct), Times.Once);
    }
}