using noKeyCloud.Application.Security.Models;

namespace noKeyCloud.Application.Abstractions.Services;

public interface IRefreshTokenProvider
{
    string GenerateRefreshToken();
    Task StoreRefreshTokenAsync(Guid userId, Guid sessionId, string refreshToken, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<RefreshTokenInfo?> FindByTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}