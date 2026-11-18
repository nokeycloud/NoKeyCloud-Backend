using Microsoft.EntityFrameworkCore;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Shared;
using noKeyCloud.Domain.Entities;
using System.Security.Cryptography.Xml;

namespace noKeyCloud.Infrastructure.Repositories;

public class FolderRepository : IFolderRepository
{
    private readonly DataContext _context;

    public FolderRepository(DataContext context)
    {
        _context = context;
    }

    public async Task<Folder?> GetFolderByFolderId(Guid folderId, CancellationToken cancellationToken)
    {
        return await _context.Folders.FirstOrDefaultAsync(u => u.Id == folderId,
            cancellationToken);
    }

    public async Task<(List<Domain.Entities.File>, List<Folder>)> GetFilesOrContentAsync(Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        var folder = await _context.Folders
               .Include(f => f.Files)
               .Include(f => f.SubFolders)
               .FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId, cancellationToken);
        if (folder == null)
        {
            throw new Exception("Folder not found");
        }
        return (folder.Files.ToList(), folder.SubFolders.ToList());
    }

    public async Task<Folder> AddFolder(Folder folder, CancellationToken cancellationToken = default)
    {
        await _context.Folders.AddAsync(folder, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return folder;
    }
    public async Task<Folder> GetUserHomeFolder(Guid UserId, CancellationToken cancellationToken)
    {
        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.UserId == UserId && f.ParentFolderId == null, cancellationToken);
        return folder;
    }
    
    public async Task<Folder?> GetByIdAndUserIdAsync(Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == folderId && x.UserId == userId, cancellationToken);
    }

    public async Task<(bool,string)> SoftDeleteFoldersById(List<Guid> foldersId, Guid userId, byte[] encryptedKey, CancellationToken cancellationToken)
    {
        var ids = foldersId.Distinct().ToList();

        var folders = await _context.Folders
            .Where(f => f.UserId == userId && !f.IsDeleted && ids.Contains(f.Id))
            .ToListAsync(cancellationToken);

        if (folders.Count != ids.Count) return (false, "Not all folders were found");

        foreach (var folder in folders)
        {
            var trash = new Trash(
                id: folder.Id,
                encryptedName: folder.EncryptedName,
                sizeBytes: 0,
                deletedAt: DateTime.UtcNow,
                type: TrashType.Folder,
                encryptedKey: encryptedKey,
                userId: folder.UserId
                );
            folder.SoftDelete();
            await _context.Trashes.AddAsync(trash, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        return (true,"Folders successfully deleted");
    }

    public async Task<(bool,string)> GetFolderAndSubFoldersById(List<Guid> foldersId, IReadOnlyList<Guid> folderIds, Guid userId, CancellationToken cancellationToken)
    {
        foreach (var folderId in folderIds.Distinct())
        {
            var response = await CollectFolderAndSubFolderIds(foldersId, folderId, userId, cancellationToken);
            if (!response.Item1) return response;
        }

        return (true, "Folders successfully found");
    }

    private async Task<(bool, string)> CollectFolderAndSubFolderIds(List<Guid> foldersId, Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        var folder = await _context.Folders
            .Include(f => f.SubFolders)
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Id == folderId, cancellationToken);

        if (folder == null) return (false, "Could not find folder");

        if (folder.ParentFolderId == null) return (false, "You can not delete root folder");

        if (!foldersId.Contains(folder.Id)) foldersId.Add(folder.Id);

        foreach (var subFolder in folder.SubFolders)
        {
            var response = await CollectFolderAndSubFolderIds(foldersId, subFolder.Id, userId, cancellationToken);
            if (!response.Item1) return response;
        }

        return (true, "Folders successfully found");
    }
}