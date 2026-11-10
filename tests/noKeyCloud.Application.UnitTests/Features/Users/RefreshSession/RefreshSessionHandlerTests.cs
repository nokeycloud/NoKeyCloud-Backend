using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Application.Features.Users.RefreshSession;
using noKeyCloud.Application.Security.Models;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users.RefreshSession;

public class RefreshSessionHandlerTests
{
    private const string OldToken = "old-valid-token";
    private const string NewToken = "new-refresh-token";
    private const string JwtToken = "new-jwt-token";

    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IRefreshTokenProvider> _refreshTokenProviderMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly RefreshSessionHandler _handler;
    
    private static readonly JwtOptions DefaultJwtOptions = new()
    {
        SecretKey = "unused-in-tests",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeHours = 24
    };

    public RefreshSessionHandlerTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _refreshTokenProviderMock = new Mock<IRefreshTokenProvider>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _handler = new RefreshSessionHandler(
            _jwtServiceMock.Object,
            _refreshTokenProviderMock.Object,
            _userRepositoryMock.Object,
            Options.Create(DefaultJwtOptions));
    }

    private static User CreateFakeUser(Guid id, bool isAdmin = false) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@test.com",
            username: "test",
            registrationRecord: "record",
            encryptedMasterKey: [1],
            keySalt: [2],
            isAdmin: isAdmin);

    private void SetupHappyPath(Guid? userId = null, Guid? sessionId = null, bool isAdmin = false)
    {
        var uid = userId ?? Guid.NewGuid();
        var sid = sessionId ?? Guid.NewGuid();

        _refreshTokenProviderMock
            .Setup(p => p.FindByTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(uid, sid));
        _refreshTokenProviderMock
            .Setup(p => p.GenerateRefreshToken())
            .Returns(NewToken);
        _refreshTokenProviderMock
            .Setup(p => p.StoreRefreshTokenAsync(uid, sid, NewToken, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeUser(uid, isAdmin));
        _jwtServiceMock
            .Setup(j => j.JwtTokenService(uid, isAdmin, sid))
            .ReturnsAsync(JwtToken);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldReturnNewTokens()
    {
        SetupHappyPath();
        var result = await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewToken, result.Value!.RefreshToken);
        Assert.Equal(JwtToken, result.Value.JwtToken);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldRotateWithinSameSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupHappyPath(userId, sessionId);

        await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(userId, sessionId, NewToken, TimeSpan.FromHours(24), It.IsAny<CancellationToken>()),
            Times.Once);
        _refreshTokenProviderMock.Verify(
            p => p.RevokeSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldPassSameSessionIdToJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupHappyPath(userId, sessionId);

        await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        _jwtServiceMock.Verify(j => j.JwtTokenService(userId, It.IsAny<bool>(), sessionId), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenNotFound_ShouldReturnFailure()
    {
        _refreshTokenProviderMock
            .Setup(p => p.FindByTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenInfo?)null);

        var result = await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid or expired refresh token.", result.Error);
        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenGenerateRefreshTokenReturnsEmpty_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        _refreshTokenProviderMock
            .Setup(p => p.FindByTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));
        _refreshTokenProviderMock
            .Setup(p => p.GenerateRefreshToken())
            .Returns(string.Empty);

        var result = await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to generate a new refresh token.", result.Error);
        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserMissing_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        _refreshTokenProviderMock
            .Setup(p => p.FindByTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenInfo(userId, sessionId));
        _refreshTokenProviderMock
            .Setup(p => p.GenerateRefreshToken())
            .Returns(NewToken);
        _refreshTokenProviderMock
            .Setup(p => p.StoreRefreshTokenAsync(userId, sessionId, NewToken, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenGenerateJwtReturnsEmpty_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupHappyPath(userId, sessionId);
        _jwtServiceMock
            .Setup(j => j.JwtTokenService(userId, false, sessionId))
            .ReturnsAsync(string.Empty);

        var result = await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to generate a new JWT token.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_ShouldRequestAdminJwt()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupHappyPath(userId, sessionId, isAdmin: true);

        await _handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        _jwtServiceMock.Verify(j => j.JwtTokenService(userId, true, sessionId), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenLifetimeHoursIsConfigured_ShouldUseConfiguredValue()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupHappyPath(userId, sessionId);

        const int configuredHours = 6;
        var handler = new RefreshSessionHandler(
            _jwtServiceMock.Object,
            _refreshTokenProviderMock.Object,
            _userRepositoryMock.Object,
            Options.Create(new JwtOptions
            {
                SecretKey = "unused-in-tests",
                AccessTokenLifetimeMinutes = 15,
                RefreshTokenLifetimeHours = configuredHours
            }));

        await handler.Handle(new RefreshSessionCommand(OldToken), CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(
                userId, sessionId, NewToken, TimeSpan.FromHours(configuredHours), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}