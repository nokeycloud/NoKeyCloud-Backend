using Microsoft.Extensions.Caching.Distributed;
using noKeyCloud.Application.Abstractions.Services;

namespace noKeyCloud.Infrastructure.Services;

public class RedisUploadSessionStore(IDistributedCache cache) : IUploadSessionStore
{
    private const string KeyPrefix = "UploadSession_";

    public async Task SaveSessionAsync(
        Guid sessionId,
        string state,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        await cache.SetStringAsync(
            $"{KeyPrefix}{sessionId}",
            state,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry },
            cancellationToken);
    }

    public async Task<string?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await cache.GetStringAsync($"{KeyPrefix}{sessionId}", cancellationToken);
    }

    public async Task<bool> RemoveSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync($"{KeyPrefix}{sessionId}", cancellationToken);
        return true;
    }
}