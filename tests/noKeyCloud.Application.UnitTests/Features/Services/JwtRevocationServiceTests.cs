using Microsoft.Extensions.Options;
using Moq;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Infrastructure.Services;
using StackExchange.Redis;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class JwtRevocationServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int MaxLifetimeMinutes = 15;

    private readonly Mock<IDatabase> _dbMock;
    private readonly JwtRevocationService _service;

    public JwtRevocationServiceTests()
    {
        _dbMock = new Mock<IDatabase>();
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _service = new JwtRevocationService(
            multiplexerMock.Object,
            Options.Create(new JwtOptions
            {
                SecretKey = "unused",
                AccessTokenLifetimeMinutes = MaxLifetimeMinutes,
                RefreshTokenLifetimeHours = 24
            }));
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