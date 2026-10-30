using Microsoft.Extensions.Options;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using StackExchange.Redis;

namespace noKeyCloud.Infrastructure.Services;

public class JwtRevocationService(IConnectionMultiplexer redis, IOptions<JwtOptions> jwtOptions)
    : IJwtRevocationService
{
    private const string KeyPrefix = "jwt-denylist:";
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly TimeSpan _maxTtl = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return;

        cancellationToken.ThrowIfCancellationRequested();

        var effectiveTtl = ttl > _maxTtl ? _maxTtl : ttl;
        if (effectiveTtl <= TimeSpan.Zero) return;
 
        await _db.StringSetAsync($"{KeyPrefix}{jti}", "1", effectiveTtl);
        await _db.StringSetAsync($"{KeyPrefix}{jti}", "1", effectiveTtl, false, When.Always, CommandFlags.None);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        cancellationToken.ThrowIfCancellationRequested();

        return await _db.KeyExistsAsync($"{KeyPrefix}{jti}");
    }
}