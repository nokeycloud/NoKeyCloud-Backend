namespace noKeyCloud.Application.Abstractions.Services;

public interface IUploadSessionStore
{
    Task SaveSessionAsync(
        Guid sessionId,
        string state,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<string?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
    
    Task<bool> RemoveSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}