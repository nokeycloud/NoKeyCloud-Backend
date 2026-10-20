namespace noKeyCloud.Application.Abstractions.Services;

public interface IOpaqueSessionStore
{
    Task SaveSessionAsync(Guid sessionId, Guid userId, string serverLoginState, TimeSpan? expiry = null);
    Task<string?> GetServerLoginStateAsync(Guid sessionId);
    Task<Guid?> GetUserIdAsync(Guid sessionId);
    Task<bool> DeleteSessionAsync(Guid sessionId);
}