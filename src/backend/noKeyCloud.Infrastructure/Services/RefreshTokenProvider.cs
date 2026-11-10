using System.Security.Cryptography;
using System.Text;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Security.Models;
using StackExchange.Redis;

namespace noKeyCloud.Infrastructure.Services;

public class RefreshTokenProvider(IConnectionMultiplexer redis) : IRefreshTokenProvider
{
    private const string TokenKeyPrefix = "RefreshToken";
    private const string LookupKeyPrefix = "RefreshTokenLookup";
    private const string UserSessionsKeyPrefix = "UserRefreshSessions";

    private readonly IDatabase _db = redis.GetDatabase();

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private static string TokenKey(Guid userId, Guid sessionId)
        => $"{TokenKeyPrefix}:{userId}:{sessionId}";

    private static string LookupKey(string tokenHash)
        => $"{LookupKeyPrefix}:{tokenHash}";

    private static string UserSessionsKey(Guid userId)
        => $"{UserSessionsKeyPrefix}:{userId}";

    public async Task StoreRefreshTokenAsync(
        Guid userId,
        Guid sessionId,
        string refreshToken,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var newHash = HashToken(refreshToken);
        var tokenKey = TokenKey(userId, sessionId);
        var existingHashValue = await _db.StringGetAsync(tokenKey);
        if (existingHashValue.HasValue)
        {
            var existingHash = existingHashValue.ToString();
            if (!string.IsNullOrEmpty(existingHash) && existingHash != newHash)
            {
                await _db.KeyDeleteAsync(LookupKey(existingHash));
            }
        }

        var sessionRef = $"{userId}:{sessionId}";

        await _db.StringSetAsync((RedisKey)tokenKey, (RedisValue)newHash, expiry, false, When.Always, CommandFlags.None);
        await _db.StringSetAsync((RedisKey)LookupKey(newHash), (RedisValue)sessionRef, expiry, false, When.Always, CommandFlags.None);
        await _db.SetAddAsync((RedisKey)UserSessionsKey(userId), (RedisValue)sessionId.ToString(), CommandFlags.None);
        await _db.KeyExpireAsync((RedisKey)UserSessionsKey(userId), expiry, CommandFlags.None);
    }

    public async Task<RefreshTokenInfo?> FindByTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken)) return null;

        var tokenHash = HashToken(refreshToken);
        var sessionRefValue = await _db.StringGetAsync(LookupKey(tokenHash));

        if (!sessionRefValue.HasValue) return null;

        var sessionRef = sessionRefValue.ToString();
        var parts = sessionRef.Split(':');
        if (parts.Length != 2
            || !Guid.TryParse(parts[0], out var userId)
            || !Guid.TryParse(parts[1], out var sessionId))
        {
            return null;
        }

        var storedHashValue = await _db.StringGetAsync(TokenKey(userId, sessionId));
        if (!storedHashValue.HasValue || storedHashValue.ToString() != tokenHash)
        {
            return null;
        }

        return new RefreshTokenInfo(userId, sessionId);
    }

    public async Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var tokenKey = TokenKey(userId, sessionId);
        var existingHashValue = await _db.StringGetAsync(tokenKey);

        await _db.KeyDeleteAsync(tokenKey);

        if (existingHashValue.HasValue)
        {
            var existingHash = existingHashValue.ToString();
            if (!string.IsNullOrEmpty(existingHash))
            {
                await _db.KeyDeleteAsync(LookupKey(existingHash));
            }
        }

        await _db.SetRemoveAsync(UserSessionsKey(userId), sessionId.ToString());
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sessionsKey = UserSessionsKey(userId);
        var sessionIds = await _db.SetMembersAsync(sessionsKey);

        foreach (var sessionIdValue in sessionIds)
        {
            if (Guid.TryParse(sessionIdValue.ToString(), out var sessionId))
            {
                await RevokeSessionAsync(userId, sessionId, cancellationToken);
            }
        }

        await _db.KeyDeleteAsync(sessionsKey);
    }
}