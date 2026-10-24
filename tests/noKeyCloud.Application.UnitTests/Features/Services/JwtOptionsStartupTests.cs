using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using noKeyCloud.api;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class JwtOptionsStartupTests
{
    private static IConfiguration BuildConfig(string secretKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = secretKey,
                ["JwtSettings:ValidIssuer:0"] = "noKeyCloud.api",
                ["JwtSettings:ValidAudience:0"] = "noKeyCloud.api",
                ["JwtSettings:AccessTokenLifetimeMinutes"] = "15",
                ["JwtSettings:RefreshTokenLifetimeHours"] = "24"
            })
            .Build();

    [Fact]
    public void AddPresentation_WithShortSecretKey_ThrowsInvalidOperationException()
    {
        var config = BuildConfig("short-key");
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddPresentation(config));

        Assert.Contains("at least 32 bytes", ex.Message);
    }

    [Fact]
    public void AddPresentation_WithEmptySecretKey_ThrowsArgumentNullException()
    {
        var config = BuildConfig(string.Empty);
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddPresentation(config));
    }

    [Fact]
    public void AddPresentation_WithValidSecretKey_DoesNotThrow()
    {
        var config = BuildConfig(new string('k', 32));
        var services = new ServiceCollection();

        var ex = Record.Exception(() => services.AddPresentation(config));
        Assert.Null(ex);
    }
}