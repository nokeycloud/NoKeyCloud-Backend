using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Infrastructure.Services;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class JwtServiceTests
{
    private static JwtOptions CreateJwtOptions(
        string? secretKey = null,
        int accessTokenLifetimeMinutes = 15)
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
        var options = Options.Create(CreateJwtOptions(secretKey: string.Empty));
        var jwtService = new JwtService(options);
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => jwtService.JwtTokenService(id, false));

        Assert.Equal("Missing JWT SecretKey environment variable", exception.Message);
    }

    [Fact]
    public async Task JwtTokenService_ReturnsValidToken_WhenConfiguredCorrectly()
    {
        var options = Options.Create(CreateJwtOptions(accessTokenLifetimeMinutes: 60));
        var jwtService = new JwtService(options);
        var id = Guid.NewGuid();

        var tokenString = await jwtService.JwtTokenService(id, true);

        Assert.False(string.IsNullOrEmpty(tokenString));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        Assert.Equal(id.ToString(), token.Subject);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");
    }

    [Fact]
    public async Task JwtTokenService_WhenAccessTokenLifetimeMinutesIsSet_ExpClaimReflectsIt()
    {
        const int lifetimeMinutes = 42;
        var options = Options.Create(CreateJwtOptions(accessTokenLifetimeMinutes: lifetimeMinutes));
        var jwtService = new JwtService(options);
        var before = DateTime.UtcNow;

        var tokenString = await jwtService.JwtTokenService(Guid.NewGuid(), false);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
        var expectedExpiry = before.AddMinutes(lifetimeMinutes);
        
        var diff = (token.ValidTo - expectedExpiry).Duration();
        Assert.True(diff < TimeSpan.FromSeconds(30),
            $"Expected exp ~{expectedExpiry:O} but got {token.ValidTo:O} (diff {diff.TotalSeconds:F1}s)");
    }

    [Fact]
    public async Task JwtTokenService_WhenAccessTokenLifetimeMinutesDiffers_ExpClaimDiffers()
    {
        var options15 = Options.Create(CreateJwtOptions(accessTokenLifetimeMinutes: 15));
        var options42 = Options.Create(CreateJwtOptions(accessTokenLifetimeMinutes: 42));

        var token15 = new JwtSecurityTokenHandler().ReadJwtToken(
            await new JwtService(options15).JwtTokenService(Guid.NewGuid(), false));
        var token42 = new JwtSecurityTokenHandler().ReadJwtToken(
            await new JwtService(options42).JwtTokenService(Guid.NewGuid(), false));

        var delta = (token42.ValidTo - token15.ValidTo).TotalMinutes;
        Assert.InRange(delta, 26.0, 28.0);
    }
}