using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Infrastructure.Services;
using StackExchange.Redis;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class JwtProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int MaxLifetimeMinutes = 15;

    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly JwtProvider _service;

    public JwtProviderTests()
    {
        _dbMock = new Mock<IDatabase>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = CreateSut(accessTokenLifetimeMinutes: MaxLifetimeMinutes);
    }

    private JwtProvider CreateSut(
        string? secretKey = null,
        int accessTokenLifetimeMinutes = MaxLifetimeMinutes)
    {
        return new JwtProvider(
            _multiplexerMock.Object,
            Options.Create(CreateJwtOptions(secretKey, accessTokenLifetimeMinutes)));
    }

    private static JwtOptions CreateJwtOptions(
        string? secretKey = null,
        int accessTokenLifetimeMinutes = MaxLifetimeMinutes)
    {
        return new JwtOptions
        {
            SecretKey = secretKey ?? "A_VERY_SECRET_KEY_THAT_IS_LONG_ENOUGH_FOR_HMACSHA256",
            ValidIssuer = ["noKeyCloud.api"],
            ValidAudience = ["noKeyCloud.api"],
            AccessTokenLifetimeMinutes = accessTokenLifetimeMinutes,
            RefreshTokenLifetimeHours = 24
        };
    }

    [Fact]
    public async Task JwtTokenService_ThrowsException_WhenSecretKeyMissing()
    {
        var jwtService = CreateSut(secretKey: string.Empty);
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => jwtService.JwtTokenService(id, false, Guid.NewGuid()));

        Assert.Equal("Missing JWT SecretKey environment variable", exception.Message);
    }

    [Fact]
    public async Task JwtTokenService_ReturnsValidToken_WhenConfiguredCorrectly()
    {
        var jwtService = CreateSut(accessTokenLifetimeMinutes: 60);
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var tokenString = await jwtService.JwtTokenService(id, true, sessionId);

        Assert.False(string.IsNullOrEmpty(tokenString));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        Assert.Equal(id.ToString(), token.Subject);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");
        Assert.Contains(token.Claims, c => c.Type == JwtProvider.SessionIdClaimType && c.Value == sessionId.ToString());
    }

    [Fact]
    public async Task JwtTokenService_NonAdmin_DoesNotIncludeAdminRoleClaim()
    {
        var jwtService = CreateSut();
        var sessionId = Guid.NewGuid();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await jwtService.JwtTokenService(Guid.NewGuid(), false, sessionId));

        Assert.DoesNotContain(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");
        Assert.Contains(token.Claims, c => c.Type == JwtProvider.SessionIdClaimType && c.Value == sessionId.ToString());
    }

    [Fact]
    public async Task JwtTokenService_WhenAccessTokenLifetimeMinutesIsSet_ExpClaimReflectsIt()
    {
        const int lifetimeMinutes = 42;
        var jwtService = CreateSut(accessTokenLifetimeMinutes: lifetimeMinutes);
        var before = DateTime.UtcNow;

        var tokenString = await jwtService.JwtTokenService(Guid.NewGuid(), false, Guid.NewGuid());

        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
        var expectedExpiry = before.AddMinutes(lifetimeMinutes);

        var diff = (token.ValidTo - expectedExpiry).Duration();
        Assert.True(diff < TimeSpan.FromSeconds(30),
            $"Expected exp ~{expectedExpiry:O} but got {token.ValidTo:O} (diff {diff.TotalSeconds:F1}s)");
    }

    [Fact]
    public async Task JwtTokenService_WhenAccessTokenLifetimeMinutesDiffers_ExpClaimDiffers()
    {
        var jwtService15 = CreateSut(accessTokenLifetimeMinutes: 15);
        var jwtService42 = CreateSut(accessTokenLifetimeMinutes: 42);

        var token15 = new JwtSecurityTokenHandler().ReadJwtToken(
            await jwtService15.JwtTokenService(Guid.NewGuid(), false, Guid.NewGuid()));
        var token42 = new JwtSecurityTokenHandler().ReadJwtToken(
            await jwtService42.JwtTokenService(Guid.NewGuid(), false, Guid.NewGuid()));

        var delta = (token42.ValidTo - token15.ValidTo).TotalMinutes;
        Assert.InRange(delta, 26.0, 28.0);
    }

    [Fact]
    public async Task RevokeAsync_StoresKeyWithExpectedTtl()
    {
        TimeSpan? capturedTtl = null;

        _dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>(
                (_, _, ttl, _, _) => capturedTtl = ttl)
            .ReturnsAsync(true);

        _dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (_, _, ttl, _, _, _) => capturedTtl = ttl)
            .ReturnsAsync(true);

        await _service.RevokeAsync("abc123", TimeSpan.FromMinutes(5), Ct);

        Assert.Equal(TimeSpan.FromMinutes(5), capturedTtl);
    }

    [Fact]
    public async Task RevokeAsync_UsesExpectedRedisKey()
    {
        RedisKey capturedKey = default;

        _dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>(
                (key, _, _, _, _) => capturedKey = key)
            .ReturnsAsync(true);

        _dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (key, _, _, _, _, _) => capturedKey = key)
            .ReturnsAsync(true);

        await _service.RevokeAsync("abc123", TimeSpan.FromMinutes(5), Ct);

        Assert.Equal("jwt-denylist:abc123", capturedKey.ToString());
    }

    [Fact]
    public async Task RevokeAsync_CapsTtlAtConfiguredMaxLifetime()
    {
        TimeSpan? capturedTtl = null;

        _dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>(
                (_, _, ttl, _, _) => capturedTtl = ttl)
            .ReturnsAsync(true);

        _dbMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (_, _, ttl, _, _, _) => capturedTtl = ttl)
            .ReturnsAsync(true);

        await _service.RevokeAsync("abc", TimeSpan.FromHours(10), Ct);

        Assert.Equal(TimeSpan.FromMinutes(MaxLifetimeMinutes), capturedTtl);
    }

    [Fact]
    public async Task RevokeAsync_WithZeroTtl_DoesNotWrite()
    {
        await _service.RevokeAsync("abc", TimeSpan.Zero, Ct);

        VerifyNoStringSetCalls();
    }

    [Fact]
    public async Task RevokeAsync_WithNegativeTtl_DoesNotWrite()
    {
        await _service.RevokeAsync("abc", TimeSpan.FromMinutes(-1), Ct);

        VerifyNoStringSetCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RevokeAsync_WithNullOrWhitespaceJti_DoesNotWrite(string? jti)
    {
        await _service.RevokeAsync(jti!, TimeSpan.FromMinutes(5), Ct);

        VerifyNoStringSetCalls();
    }

    [Fact]
    public async Task IsRevokedAsync_WhenKeyExists_ReturnsTrue()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.IsRevokedAsync("abc", Ct);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRevokedAsync_WhenKeyMissing_ReturnsFalse()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var result = await _service.IsRevokedAsync("abc", Ct);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRevokedAsync_QueriesExpectedRedisKey()
    {
        RedisKey capturedKey = default;
        _dbMock
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => capturedKey = key)
            .ReturnsAsync(false);

        await _service.IsRevokedAsync("abc123", Ct);

        Assert.Equal("jwt-denylist:abc123", capturedKey.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task IsRevokedAsync_WithNullOrWhitespaceJti_ReturnsFalse(string? jti)
    {
        var result = await _service.IsRevokedAsync(jti!, Ct);

        Assert.False(result);
        _dbMock.Verify(
            d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    private void VerifyNoStringSetCalls()
    {
        _dbMock.Verify(
            d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);

        _dbMock.Verify(
            d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }
}