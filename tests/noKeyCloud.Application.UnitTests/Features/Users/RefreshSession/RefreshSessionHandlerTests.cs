using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Application.Features.Users.RefreshSession;
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

    private void SetupHappyPath(Guid userId, bool isAdmin = false)
    {
        _refreshTokenProviderMock
            .Setup(p => p.GetUserIdByRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _refreshTokenProviderMock
            .Setup(p => p.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OldToken);
        _refreshTokenProviderMock
            .Setup(p => p.GenerateRefreshToken())
            .Returns(NewToken);
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeUser(userId, isAdmin));
        _jwtServiceMock
            .Setup(j => j.JwtTokenService(userId, isAdmin))
            .ReturnsAsync(JwtToken);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldReturnNewTokens()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(NewToken, result.Value.RefreshToken);
        Assert.Equal(JwtToken, result.Value.JwtToken);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldRotateRefreshTokenByRevokingOldAndStoringNew()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

        var command = new RefreshSessionCommand(OldToken);

        await _handler.Handle(command, CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            p => p.RevokeRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()),
            Times.Once);
        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(userId, NewToken, TimeSpan.FromHours(24), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIdNotFound_ShouldReturnFailure()
    {
        _refreshTokenProviderMock
            .Setup(p => p.GetUserIdByRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid or expired refresh token.", result.Error);
        _refreshTokenProviderMock.Verify(
            p => p.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStoredTokenMismatches_ShouldReturnFailureAndNotRotate()
    {
        var userId = Guid.NewGuid();
        _refreshTokenProviderMock
            .Setup(p => p.GetUserIdByRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _refreshTokenProviderMock
            .Setup(p => p.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("different-stored-token");

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid or expired refresh token.", result.Error);
        _refreshTokenProviderMock.Verify(
            p => p.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStoredTokenEmpty_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        _refreshTokenProviderMock
            .Setup(p => p.GetUserIdByRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _refreshTokenProviderMock
            .Setup(p => p.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid or expired refresh token.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenGenerateRefreshTokenReturnsEmpty_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        _refreshTokenProviderMock
            .Setup(p => p.GetUserIdByRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _refreshTokenProviderMock
            .Setup(p => p.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OldToken);
        _refreshTokenProviderMock
            .Setup(p => p.GenerateRefreshToken())
            .Returns(string.Empty);

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to generate a new refresh token.", result.Error);
        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenGenerateJwtReturnsEmpty_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);
        _jwtServiceMock
            .Setup(j => j.JwtTokenService(userId, false))
            .ReturnsAsync(string.Empty);

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to generate a new JWT token.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_ShouldRequestAdminJwt()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId, isAdmin: true);

        var command = new RefreshSessionCommand(OldToken);

        await _handler.Handle(command, CancellationToken.None);

        _jwtServiceMock.Verify(j => j.JwtTokenService(userId, true), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotAdmin_ShouldRequestNonAdminJwt()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId, isAdmin: false);

        var command = new RefreshSessionCommand(OldToken);

        await _handler.Handle(command, CancellationToken.None);

        _jwtServiceMock.Verify(j => j.JwtTokenService(userId, false), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldReturnResponseWithFutureAccessTokenExpiry()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

        var command = new RefreshSessionCommand(OldToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.ResponsePayload);
        Assert.True(result.Value.ResponsePayload.AccessTokenExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldGenerateRefreshTokenExactlyOnce()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

        var command = new RefreshSessionCommand(OldToken);

        await _handler.Handle(command, CancellationToken.None);

        _refreshTokenProviderMock.Verify(p => p.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldLookupUserById()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

        var command = new RefreshSessionCommand(OldToken);

        await _handler.Handle(command, CancellationToken.None);

        _userRepositoryMock.Verify(
            r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ShouldNotStoreNewTokenBeforeRevokingOld()
    {
        var userId = Guid.NewGuid();
        var callOrder = new List<string>();

        _refreshTokenProviderMock
            .Setup(p => p.GetUserIdByRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _refreshTokenProviderMock
            .Setup(p => p.GetRefreshTokenAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OldToken);
        _refreshTokenProviderMock
            .Setup(p => p.GenerateRefreshToken())
            .Returns(NewToken);
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeUser(userId));
        _jwtServiceMock
            .Setup(j => j.JwtTokenService(userId, false))
            .ReturnsAsync(JwtToken);

        _refreshTokenProviderMock
            .Setup(p => p.RevokeRefreshTokenAsync(OldToken, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("revoke"))
            .Returns(Task.CompletedTask);
        _refreshTokenProviderMock
            .Setup(p => p.StoreRefreshTokenAsync(userId, NewToken, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("store"))
            .Returns(Task.CompletedTask);

        var command = new RefreshSessionCommand(OldToken);

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(new[] { "revoke", "store" }, callOrder);
    }
    
    [Fact]
    public async Task Handle_WhenAccessTokenLifetimeMinutesIsConfigured_ResponseExpiryReflectsIt()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

        const int configuredMinutes = 30;
        var handler = new RefreshSessionHandler(
            _jwtServiceMock.Object,
            _refreshTokenProviderMock.Object,
            _userRepositoryMock.Object,
            Options.Create(new JwtOptions
            {
                SecretKey = "unused-in-tests",
                AccessTokenLifetimeMinutes = configuredMinutes,
                RefreshTokenLifetimeHours = 24
            }));

        var before = DateTime.UtcNow;

        var result = await handler.Handle(
            new RefreshSessionCommand(OldToken), CancellationToken.None);

        var expected = before.AddMinutes(configuredMinutes);
        var actual = result.Value!.ResponsePayload.AccessTokenExpiresAt;

        var diff = (actual - expected).Duration();
        Assert.True(diff < TimeSpan.FromSeconds(5),
            $"Expected ~{expected:O} but got {actual:O}");
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenLifetimeHoursIsConfigured_ShouldUseConfiguredValue()
    {
        var userId = Guid.NewGuid();
        SetupHappyPath(userId);

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

        await handler.Handle(
            new RefreshSessionCommand(OldToken), CancellationToken.None);

        _refreshTokenProviderMock.Verify(
            p => p.StoreRefreshTokenAsync(
                userId,
                NewToken,
                TimeSpan.FromHours(configuredHours),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}