using noKeyCloud.Domain.Entities;
using File = noKeyCloud.Domain.Entities.File;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IFileRepository
{
    Task CreateFile(File file, CancellationToken cancellationToken, byte[]? fileContent = null);

    Task<(File file, byte[] content)> GetFileById(Guid fileId, Guid userId, CancellationToken cancellationToken);

    Task<byte[]> DownloadFile(File file, string fullPath, CancellationToken cancellationToken);
    
    Task<bool> SoftDeleteFileById(IReadOnlyList<Guid> fileIds, Guid userId, byte[] encryptedKey, CancellationToken cancellationToken);

    Task<List<Trash>> GetTrashedFilesAndFolder(Guid userId, CancellationToken cancellationToken);

    Task<DeleteFilesResult> DeleteFiles(Guid userId, List<Guid> fileIds, CancellationToken cancellationToken);

    Task<File> DoesFileEvenExists(File file, string fullPath, CancellationToken cancellationToken);


}
