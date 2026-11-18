using Microsoft.IdentityModel.Tokens;
using noKeyCloud.Application.Abstractions.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Configuration;
using StackExchange.Redis;

namespace noKeyCloud.Infrastructure.Services;

public class JwtProvider(IConnectionMultiplexer redis, IOptions<JwtOptions> jwtOptions) : IJwtProvider

{
    public const string SessionIdClaimType = "sid";
    private readonly JwtOptions _options = jwtOptions.Value;
    
    private const string KeyPrefix = "jwt-denylist:";
    private readonly IDatabase _cache = redis.GetDatabase();
    private readonly TimeSpan _maxTtl = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);
    
    public Task<string> JwtTokenService(Guid? id, bool isAdmin, Guid sessionId)
    {
        var secretKeyString = _options.SecretKey;

        if (string.IsNullOrEmpty(secretKeyString))
        {
            throw new InvalidOperationException("Missing JWT SecretKey environment variable");
        }

        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyString));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(SessionIdClaimType, sessionId.ToString())
        };

        if (isAdmin)
        {
            claims = [.. claims, new Claim(ClaimTypes.Role, "admin")];
        }

        var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.ValidIssuer.FirstOrDefault(),
            audience: _options.ValidAudience.FirstOrDefault(),
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes),
            signingCredentials: credentials
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }
    
    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return;

        cancellationToken.ThrowIfCancellationRequested();

        var effectiveTtl = ttl > _maxTtl ? _maxTtl : ttl;
        if (effectiveTtl <= TimeSpan.Zero) return;
 
        await _cache.StringSetAsync($"{KeyPrefix}{jti}", "1", effectiveTtl);
        await _cache.StringSetAsync($"{KeyPrefix}{jti}", "1", effectiveTtl, false, When.Always, CommandFlags.None);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        cancellationToken.ThrowIfCancellationRequested();

        return await _cache.KeyExistsAsync($"{KeyPrefix}{jti}");
    }
}
