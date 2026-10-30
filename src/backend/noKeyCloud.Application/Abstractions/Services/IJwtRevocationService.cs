namespace noKeyCloud.Application.Abstractions.Services;

public interface IJwtRevocationService
{
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}