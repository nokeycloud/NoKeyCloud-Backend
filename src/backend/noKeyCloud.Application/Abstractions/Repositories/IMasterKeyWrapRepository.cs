using noKeyCloud.Domain.Entities;
using noKeyCloud.Domain.Enums;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IMasterKeyWrapRepository
{
    void Add(MasterKeyWrap masterKeyWrap);
    void Delete(MasterKeyWrap masterKeyWrap);
    Task<MasterKeyWrap?> GetByUserIdAndTypeAsync(Guid userId, MasterKeyWrapType masterKeyWrapType,CancellationToken cancellationToken = default);
    void Update(MasterKeyWrap masterKeyWrap);
}