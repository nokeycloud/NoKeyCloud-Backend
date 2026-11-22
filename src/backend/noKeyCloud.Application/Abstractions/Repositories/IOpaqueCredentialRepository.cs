using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IOpaqueCredentialRepository
{
    void Add(OpaqueCredential credential);
    void Delete(OpaqueCredential credential);
    Task<OpaqueCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Update(OpaqueCredential credential);
}