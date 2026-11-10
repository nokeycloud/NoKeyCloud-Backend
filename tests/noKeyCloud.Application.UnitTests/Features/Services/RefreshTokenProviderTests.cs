using Moq;
using noKeyCloud.Infrastructure.Services;
using StackExchange.Redis;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class RefreshTokenProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Dictionary<string, string> _strings = new();
    private readonly Dictionary<string, HashSet<string>> _sets = new();
    private readonly RefreshTokenProvider _provider;

    public RefreshTokenProviderTests()
    {
        var dbMock = new Mock<IDatabase>();
        var redisMock = new Mock<IConnectionMultiplexer>();

        redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        dbMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                _strings.TryGetValue(key.ToString(), out var v) ? (RedisValue)v : RedisValue.Null);

        dbMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue value, TimeSpan? _, bool _, When _, CommandFlags _) =>
            {
                _strings[key.ToString()] = value.ToString();
                return true;
            });

        dbMock
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
            {
                var stringRemoved = _strings.Remove(key.ToString());
                var setRemoved = _sets.Remove(key.ToString());
                return stringRemoved || setRemoved;
            });

        dbMock
            .Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags _) =>
            {
                if (_sets.TryGetValue(key.ToString(), out var set)) return set.Add(value.ToString());
                set = [];
                _sets[key.ToString()] = set;
                return set.Add(value.ToString());
            });

        dbMock
            .Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags _) =>
                _sets.TryGetValue(key.ToString(), out var set) && set.Remove(value.ToString()));

        dbMock
            .Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                _sets.TryGetValue(key.ToString(), out var set)
                    ? [.. set.Select(s => (RedisValue)s)]
                    : []);

        dbMock
            .Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _provider = new RefreshTokenProvider(redisMock.Object);
    }

    private static string UserSessionsKey(Guid userId) => $"UserRefreshSessions:{userId}";
    private static string TokenKey(Guid userId, Guid sessionId) => $"RefreshToken:{userId}:{sessionId}";

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyBase64()
    {
        var token = _provider.GenerateRefreshToken();
        Assert.False(string.IsNullOrWhiteSpace(token));
        var bytes = Convert.FromBase64String(token);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldBeUniqueAcrossManyCalls()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            seen.Add(_provider.GenerateRefreshToken());
        }

        Assert.Equal(100, seen.Count);
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldStoreHashNotRawToken()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string rawToken = "super-secret-raw-token";

        await _provider.StoreRefreshTokenAsync(userId, sessionId, rawToken, TimeSpan.FromHours(24), Ct);

        foreach (var (k, v) in _strings)
        {
            Assert.DoesNotContain(rawToken, k);
            Assert.DoesNotContain(rawToken, v);
        }
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldCreateTokenAndLookupKeys()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string rawToken = "tok";

        await _provider.StoreRefreshTokenAsync(userId, sessionId, rawToken, TimeSpan.FromHours(24), Ct);

        Assert.True(_strings.ContainsKey(TokenKey(userId, sessionId)));
        Assert.Contains(_strings.Keys, k => k.StartsWith("RefreshTokenLookup:"));
        Assert.True(_sets.ContainsKey(UserSessionsKey(userId)));
        Assert.Contains(sessionId.ToString(), _sets[UserSessionsKey(userId)]);
    }

    [Fact]
    public async Task FindByTokenAsync_WhenTokenStored_ShouldReturnUserAndSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string rawToken = "valid-token";
        await _provider.StoreRefreshTokenAsync(userId, sessionId, rawToken, TimeSpan.FromHours(24), Ct);

        var info = await _provider.FindByTokenAsync(rawToken, Ct);

        Assert.NotNull(info);
        Assert.Equal(userId, info.UserId);
        Assert.Equal(sessionId, info.SessionId);
    }

    [Fact]
    public async Task FindByTokenAsync_WhenTokenUnknown_ShouldReturnNull()
    {
        var info = await _provider.FindByTokenAsync("nope", Ct);
        Assert.Null(info);
    }

    [Fact]
    public async Task FindByTokenAsync_WhenTokenEmpty_ShouldReturnNull()
    {
        Assert.Null(await _provider.FindByTokenAsync(string.Empty, Ct));
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldReplaceOldHashForSameSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, sessionId, "old", TimeSpan.FromHours(24), Ct);
        var oldLookupKeys = _strings.Keys.Where(k => k.StartsWith("RefreshTokenLookup:")).ToList();
        Assert.Single(oldLookupKeys);

        await _provider.StoreRefreshTokenAsync(userId, sessionId, "new", TimeSpan.FromHours(24), Ct);

        Assert.Single(_strings.Keys, k => k.StartsWith("RefreshTokenLookup:"));
        Assert.Null(await _provider.FindByTokenAsync("old", Ct));
        Assert.NotNull(await _provider.FindByTokenAsync("new", Ct));
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldNotAffectOtherSessionsOfSameUser()
    {
        var userId = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();

        await _provider.StoreRefreshTokenAsync(userId, sessionA, "tokenA", TimeSpan.FromHours(24), Ct);
        await _provider.StoreRefreshTokenAsync(userId, sessionB, "tokenB", TimeSpan.FromHours(24), Ct);

        Assert.NotNull(await _provider.FindByTokenAsync("tokenA", Ct));
        Assert.NotNull(await _provider.FindByTokenAsync("tokenB", Ct));
    }

    [Fact]
    public async Task RevokeSessionAsync_ShouldRemoveOnlyThatSession()
    {
        var userId = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, sessionA, "tokenA", TimeSpan.FromHours(24), Ct);
        await _provider.StoreRefreshTokenAsync(userId, sessionB, "tokenB", TimeSpan.FromHours(24), Ct);

        await _provider.RevokeSessionAsync(userId, sessionA, Ct);

        Assert.Null(await _provider.FindByTokenAsync("tokenA", Ct));
        Assert.NotNull(await _provider.FindByTokenAsync("tokenB", Ct));
        Assert.False(_strings.ContainsKey(TokenKey(userId, sessionA)));
        Assert.True(_strings.ContainsKey(TokenKey(userId, sessionB)));
    }

    [Fact]
    public async Task RevokeSessionAsync_WhenUnknownSession_ShouldNotThrow()
    {
        var ex = await Record.ExceptionAsync(
            () => _provider.RevokeSessionAsync(Guid.NewGuid(), Guid.NewGuid(), Ct));
        Assert.Null(ex);
    }

    [Fact]
    public async Task RevokeAllSessionsAsync_ShouldRemoveEverySessionForUser()
    {
        var userId = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, sessionA, "tokenA", TimeSpan.FromHours(24), Ct);
        await _provider.StoreRefreshTokenAsync(userId, sessionB, "tokenB", TimeSpan.FromHours(24), Ct);

        await _provider.RevokeAllSessionsAsync(userId, Ct);

        Assert.Null(await _provider.FindByTokenAsync("tokenA", Ct));
        Assert.Null(await _provider.FindByTokenAsync("tokenB", Ct));
        Assert.False(_sets.ContainsKey(UserSessionsKey(userId)));
    }

    [Fact]
    public async Task RevokeAllSessionsAsync_ShouldNotAffectOtherUsers()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userA, Guid.NewGuid(), "tokenA", TimeSpan.FromHours(24), Ct);
        await _provider.StoreRefreshTokenAsync(userB, Guid.NewGuid(), "tokenB", TimeSpan.FromHours(24), Ct);

        await _provider.RevokeAllSessionsAsync(userA, Ct);

        Assert.Null(await _provider.FindByTokenAsync("tokenA", Ct));
        Assert.NotNull(await _provider.FindByTokenAsync("tokenB", Ct));
    }

    [Fact]
    public async Task RevokeAllSessionsAsync_WhenUserHasNoSessions_ShouldNotThrow()
    {
        var ex = await Record.ExceptionAsync(
            () => _provider.RevokeAllSessionsAsync(Guid.NewGuid(), Ct));
        Assert.Null(ex);
    }

    [Fact]
    public async Task FindByTokenAsync_WhenLookupExistsButSessionKeyMissing_ShouldReturnNull()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, sessionId, "tok", TimeSpan.FromHours(24), Ct);
        _strings.Remove(TokenKey(userId, sessionId));

        Assert.Null(await _provider.FindByTokenAsync("tok", Ct));
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldExtendUserSessionsTtl()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, sessionId, "tok", TimeSpan.FromHours(24), Ct);

        Assert.True(_sets.ContainsKey(UserSessionsKey(userId)));
    }
}