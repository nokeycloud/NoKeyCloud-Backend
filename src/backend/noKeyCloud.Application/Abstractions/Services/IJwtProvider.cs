namespace noKeyCloud.Application.Abstractions.Services
{
    public interface IJwtProvider
    {
        Task<string> JwtTokenService(Guid? id, bool isAdmin, Guid sessionId);
        Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default);
        Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
    }
}
