using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using noKeyCloud.Infrastructure.Services;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class RefreshTokenProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Dictionary<string, byte[]> _store;
    private readonly RefreshTokenProvider _provider;

    public RefreshTokenProviderTests()
    {
        _store = new Dictionary<string, byte[]>();
        _cacheMock = new Mock<IDistributedCache>();

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, value, _, _) => _store[key] = value)
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) =>
                Task.FromResult(_store.GetValueOrDefault(key)!)!);

        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => _store.Remove(key))
            .Returns(Task.CompletedTask);

        _provider = new RefreshTokenProvider(_cacheMock.Object);
    }

    private static string ReverseIndexKey(string token) => $"TokenUser_{token}";
    private static string UserIndexKey(Guid userId) => $"RefreshToken_{userId}";

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
    public async Task StoreRefreshTokenAsync_WhenNoExistingToken_ShouldSetBothKeys()
    {
        var userId = Guid.NewGuid();

        await _provider.StoreRefreshTokenAsync(userId, "new-token", TimeSpan.FromHours(24), Ct);

        Assert.True(_store.ContainsKey(UserIndexKey(userId)));
        Assert.True(_store.ContainsKey(ReverseIndexKey("new-token")));
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenNoExistingToken_ShouldNotIssueAnyRemoveCall()
    {
        var userId = Guid.NewGuid();

        await _provider.StoreRefreshTokenAsync(userId, "new-token", TimeSpan.FromHours(24), Ct);

        _cacheMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenExistingToken_ShouldRemoveOldReverseIndexOnly()
    {
        var userId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, "old-token", TimeSpan.FromHours(24), Ct);

        _cacheMock.Invocations.Clear();

        await _provider.StoreRefreshTokenAsync(userId, "new-token", TimeSpan.FromHours(24), Ct);

        _cacheMock.Verify(
            c => c.RemoveAsync(ReverseIndexKey("old-token"), It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheMock.Verify(
            c => c.RemoveAsync(UserIndexKey(userId), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenExistingToken_ShouldOverwriteUserIndexWithNewToken()
    {
        var userId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, "old-token", TimeSpan.FromHours(24), Ct);

        await _provider.StoreRefreshTokenAsync(userId, "new-token", TimeSpan.FromHours(24), Ct);

        var stored = await _provider.GetRefreshTokenAsync(userId, Ct);
        Assert.Equal("new-token", stored);
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldUseProvidedExpiryForBothKeys()
    {
        var userId = Guid.NewGuid();
        var expiry = TimeSpan.FromMinutes(42);

        await _provider.StoreRefreshTokenAsync(userId, "tok", expiry, Ct);

        _cacheMock.Verify(c => c.SetAsync(
            UserIndexKey(userId),
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == expiry),
            It.IsAny<CancellationToken>()), Times.Once);

        _cacheMock.Verify(c => c.SetAsync(
            ReverseIndexKey("tok"),
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == expiry),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRefreshTokenAsync_WhenKeyExists_ShouldReturnStoredToken()
    {
        var userId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, "stored-token", TimeSpan.FromHours(24), Ct);

        var result = await _provider.GetRefreshTokenAsync(userId, Ct);

        Assert.Equal("stored-token", result);
    }

    [Fact]
    public async Task GetRefreshTokenAsync_WhenKeyMissing_ShouldReturnNull()
    {
        var result = await _provider.GetRefreshTokenAsync(Guid.NewGuid(), Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserIdByRefreshTokenAsync_WhenTokenStored_ShouldReturnUserId()
    {
        var userId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, "some-token", TimeSpan.FromHours(24), Ct);

        var result = await _provider.GetUserIdByRefreshTokenAsync("some-token", Ct);

        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task GetUserIdByRefreshTokenAsync_WhenTokenMissing_ShouldReturnNull()
    {
        var result = await _provider.GetUserIdByRefreshTokenAsync("missing-token", Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserIdByRefreshTokenAsync_WhenStoredValueIsNotGuid_ShouldReturnNull()
    {
        _store[ReverseIndexKey("bad-token")] = Encoding.UTF8.GetBytes("not-a-guid");

        var result = await _provider.GetUserIdByRefreshTokenAsync("bad-token", Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task InvalidateRefreshTokenAsync_WhenTokenExists_ShouldRemoveBothKeys()
    {
        var userId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, "token-to-invalidate", TimeSpan.FromHours(24), Ct);

        await _provider.InvalidateRefreshTokenAsync(userId, Ct);

        Assert.False(_store.ContainsKey(UserIndexKey(userId)));
        Assert.False(_store.ContainsKey(ReverseIndexKey("token-to-invalidate")));
    }

    [Fact]
    public async Task InvalidateRefreshTokenAsync_WhenNoToken_ShouldOnlyRemoveUserIndex()
    {
        var userId = Guid.NewGuid();

        await _provider.InvalidateRefreshTokenAsync(userId, Ct);

        _cacheMock.Verify(
            c => c.RemoveAsync(UserIndexKey(userId), It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheMock.Verify(
            c => c.RemoveAsync(It.Is<string>(k => k.StartsWith("TokenUser_")), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvalidateRefreshTokenAsync_ShouldNotThrowWhenNothingWasStored()
    {
        var userId = Guid.NewGuid();

        var exception = await Record.ExceptionAsync(() => _provider.InvalidateRefreshTokenAsync(userId, Ct));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WhenTokenValid_ShouldRemoveBothKeys()
    {
        var userId = Guid.NewGuid();
        await _provider.StoreRefreshTokenAsync(userId, "revocable-token", TimeSpan.FromHours(24), Ct);

        await _provider.RevokeRefreshTokenAsync("revocable-token", Ct);

        Assert.False(_store.ContainsKey(UserIndexKey(userId)));
        Assert.False(_store.ContainsKey(ReverseIndexKey("revocable-token")));
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WhenTokenUnknown_ShouldNotIssueAnyRemoveCall()
    {
        await _provider.RevokeRefreshTokenAsync("unknown-token", Ct);

        _cacheMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WhenTokenUnknown_ShouldNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => _provider.RevokeRefreshTokenAsync("unknown-token", Ct));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ShouldNotRemoveAnotherUsersToken()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await _provider.StoreRefreshTokenAsync(ownerId, "owner-token", TimeSpan.FromHours(24), Ct);
        await _provider.StoreRefreshTokenAsync(otherUserId, "other-token", TimeSpan.FromHours(24), Ct);

        await _provider.RevokeRefreshTokenAsync("owner-token", Ct);

        Assert.False(_store.ContainsKey(UserIndexKey(ownerId)));
        Assert.False(_store.ContainsKey(ReverseIndexKey("owner-token")));

        Assert.True(_store.ContainsKey(UserIndexKey(otherUserId)));
        Assert.True(_store.ContainsKey(ReverseIndexKey("other-token")));
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldForwardCancellationToken()
    {
        var userId = Guid.NewGuid();

        await _provider.StoreRefreshTokenAsync(userId, "tok", TimeSpan.FromHours(24), Ct);

        _cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                Ct),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetRefreshTokenAsync_ShouldForwardCancellationToken()
    {
        var userId = Guid.NewGuid();

        await _provider.GetRefreshTokenAsync(userId, Ct);

        _cacheMock.Verify(c => c.GetAsync(UserIndexKey(userId), Ct), Times.Once);
    }
}