using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IRegisterInviteRepository
{
    void Add(RegisterInvite invite);
    Task<RegisterInvite?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Update(RegisterInvite invite);
    Task<IEnumerable<RegisterInvite>> GetActiveInvitesAsync(CancellationToken cancellationToken = default);
    Task<RegisterInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Delete(RegisterInvite invite);
}