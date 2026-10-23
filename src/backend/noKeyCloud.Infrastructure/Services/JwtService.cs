using Microsoft.IdentityModel.Tokens;
using noKeyCloud.Application.Abstractions.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Configuration;

namespace noKeyCloud.Infrastructure.Services;

public class JwtService(IOptions<JwtOptions> jwtOptions) : IJwtService

{
    private readonly JwtOptions _options = jwtOptions.Value;
    public Task<string> JwtTokenService(Guid? id, bool isAdmin)
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
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
}
