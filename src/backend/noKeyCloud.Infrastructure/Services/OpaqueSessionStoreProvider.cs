using noKeyCloud.Application.Abstractions.Services;
using StackExchange.Redis;

namespace noKeyCloud.Infrastructure.Services;

public class OpaqueSessionStoreProvider(IConnectionMultiplexer redis) : IOpaqueSessionStore
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task SaveSessionAsync(Guid sessionId, Guid userId, string serverLoginState, TimeSpan? expiry = null)
    {
        var ttl = expiry ?? TimeSpan.FromMinutes(5);
        await _db.StringSetAsync($"opaque:state:{sessionId}", serverLoginState, ttl);
        await _db.StringSetAsync($"opaque:user:{sessionId}", userId.ToString(), ttl);
    }

    public async Task<string?> GetServerLoginStateAsync(Guid sessionId)
    {
        var val = await _db.StringGetAsync($"opaque:state:{sessionId}");
        return val.IsNullOrEmpty ? null : val.ToString();
    }

    public async Task<Guid?> GetUserIdAsync(Guid sessionId)
    {
        var val = await _db.StringGetAsync($"opaque:user:{sessionId}");
        return val.IsNullOrEmpty ? null : Guid.Parse(val.ToString()!);
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId)
    {
        var stateKey = $"opaque:state:{sessionId}";
        var userKey  = $"opaque:user:{sessionId}";
        
        var batch = _db.CreateBatch();
        var delState = batch.KeyDeleteAsync(stateKey);
        var delUser  = batch.KeyDeleteAsync(userKey);
        batch.Execute();
        await Task.WhenAll(delState, delUser);

        return !await _db.KeyExistsAsync(stateKey);
    }
}