using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using noKeyCloud.Application.Configuration;

namespace noKeyCloud.api;

public static class DependencyInjection
{
    /// <summary>
    /// Configures presentation layer services, including JWT authentication.
    /// </summary>
    /// <param name="configuration">Builder configuration instance</param>
    public static IServiceCollection AddPresentation(this IServiceCollection services,  IConfiguration configuration)
    {
        
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                         ?? throw new InvalidOperationException(
                             $"JWT configuration section '{JwtOptions.SectionName}' is missing.");
        var secretKey = jwtOptions.SecretKey;

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new ArgumentNullException(nameof(secretKey), "JWT secret key must be provided in environment variables.");
        }
        
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("CRITICAL RUNTIME ERROR: The 'JwtSettings__SecretKey' provided in your .env configuration is too short. " +
                                                "Symmetric HS256 JWT keys must be at least 32 bytes (256-bits). " +
                                                $"Your current key is only {keyBytes.Length} bytes long. Please generate a longer secure string.");
        }

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.ContainsKey("access_token"))
                        {
                            context.Token = context.Request.Cookies["access_token"];
                        }
                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuers = jwtOptions.ValidIssuer,
                    ValidAudiences = jwtOptions.ValidAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        
        return services;
    }
}