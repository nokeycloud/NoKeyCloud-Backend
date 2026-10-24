namespace noKeyCloud.Application.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string[] ValidIssuer { get; set; } = [];
    public string[] ValidAudience { get; set; } = [];
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeHours { get; set; } = 24;
}