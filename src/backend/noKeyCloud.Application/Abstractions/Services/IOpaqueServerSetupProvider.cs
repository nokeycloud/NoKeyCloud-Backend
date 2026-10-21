namespace noKeyCloud.Application.Abstractions.Services;

public interface IOpaqueServerSetupProvider
{
    Task<string> GetServerSetupAsync(CancellationToken cancellationToken);
    Task<string> GetServerPublicKeyAsync(CancellationToken cancellationToken);
}