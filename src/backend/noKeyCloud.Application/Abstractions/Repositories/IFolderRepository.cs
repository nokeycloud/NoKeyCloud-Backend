using noKeyCloud.Domain.Entities;
using File = System.IO.File;
using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IFolderRepository
{
    
    Task<Folder?> GetFolderByFolderId(Guid folderId, CancellationToken cancellationToken);
    
    Task<(List<noKeyCloud.Domain.Entities.File>, List<Folder>)> GetFilesOrContentAsync(Guid folderId, Guid userId, CancellationToken cancellationToken);

    void Add(Folder folder);

    Task<Folder> GetUserHomeFolder(Guid UserId, CancellationToken cancellationToken);
    
    Task<Folder?> GetByIdAndUserIdAsync(Guid folderId, Guid userId, CancellationToken cancellationToken);

    Task<(bool, string)> SoftDeleteFoldersById(List<Guid> foldersId, Guid userId, byte[] encryptedKey, CancellationToken cancellationToken);
    
    Task<(bool,string)> GetFolderAndSubFoldersById(List<Guid> foldersId, IReadOnlyList<Guid> folderId, Guid userId, CancellationToken cancellationToken);
}