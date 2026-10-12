using Microsoft.Extensions.Logging;
using noKeyCloud.Application.Abstractions.Services;
using OPAQUE.Net;

namespace noKeyCloud.Infrastructure.Services;

public class OpaqueServerSetupProvider(ILogger<OpaqueServerSetupProvider> logger) : IOpaqueServerSetupProvider
{
    private const string EnvVarName = "OPAQUE_SERVER_SETUP_KEY";
    private const string PathVarName   = "OPAQUE_SERVER_SETUP_KEY_PATH";
    private const string DefaultFileName = "opaque_server_setup.key";

    private string? _cachedKey;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetServerSetupAsync(CancellationToken cancellationToken)
    {
        if (_cachedKey is not null)
            return _cachedKey;
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cachedKey ??= LoadOrCreate();
            return _cachedKey;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    private string ResolveFilePath()
    {
        var explicitPath = Environment.GetEnvironmentVariable(PathVarName);
        return !string.IsNullOrWhiteSpace(explicitPath) ? explicitPath : Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    private string LoadOrCreate()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            logger.LogInformation("OPAQUE server setup key loaded from .env");
            return fromEnv.Trim();
        }
        
        var filePath = ResolveFilePath();

        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                logger.LogInformation("OPAQUE server setup key loaded from key file");
                return content;
            }
        }
        
        logger.LogCritical(
            "No OPAQUE server setup key found. Generating a new one at {Path}. " +
            "BACK UP this file: if it is lost, all existing user registrations become invalid.",
            filePath);

        var genKey = CreateServerKey();
        
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, genKey);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return genKey;
    }

    private string CreateServerKey()
    {
        var opaqueServer = new OpaqueServer();
        opaqueServer.CreateSetup(out var serverSetup);
        return serverSetup ?? throw new InvalidOperationException("Failed to create server key");
    }
    
    public async Task<string> GetServerPublicKeyAsync(CancellationToken cancellationToken)
    {
        var setup = await GetServerSetupAsync(cancellationToken);
        var server = new OpaqueServer();
        if (!server.GetPublicKey(setup, out var publicKey) || string.IsNullOrEmpty(publicKey))
        {
            throw new InvalidOperationException("Failed to derive OPAQUE server public key from setup.");
        }
        return publicKey;
    }
}