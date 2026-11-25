using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface ITrashRepository
{
    Task<List<Trash>> GetTrashItems(Guid userId, CancellationToken cancellationToken);

    Task<HardDeleteFilesResult> HardDeleteFiles(Guid userId, IReadOnlyList<Guid> fileIds, CancellationToken cancellationToken);
}
