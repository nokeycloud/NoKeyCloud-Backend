using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Shared;
using noKeyCloud.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using File = noKeyCloud.Domain.Entities.File;

namespace noKeyCloud.Infrastructure.Repositories;



public class FileRepository : IFileRepository
{
    private const string FileExtension = ".nkc";
    private readonly DataContext _context;
    private readonly ILogger<FileRepository> _logger;
    private readonly string _baseStoragePath;

    public FileRepository(DataContext context, ILogger<FileRepository> logger)
    {
        _context = context;
        _logger = logger;
        
        var envPath = Environment.GetEnvironmentVariable("BASE_UPLOAD_STORAGE_PATH");
        if (string.IsNullOrWhiteSpace(envPath))
        {
            throw new InvalidOperationException("Required environment variable 'BASE_UPLOAD_STORAGE_PATH' is missing or empty. Configure 'BASE_UPLOAD_STORAGE_PATH' before starting the application.");
        }
        _baseStoragePath = envPath;

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    private string GetFullPath(File file)
    {
        return Path.Combine(_baseStoragePath, $"{file.Id}{FileExtension}");
    }

    public async Task CreateFile(File file, CancellationToken cancellationToken, byte[]? fileContent = null)
    {
        var fullPath = GetFullPath(file);

        var exists = await _context.Files.AnyAsync(f => f.Id == file.Id, cancellationToken);

        try
        {
            if (exists)
            {
                if (fileContent != null || !System.IO.File.Exists(fullPath))
                {
                    await System.IO.File.WriteAllBytesAsync(fullPath, fileContent ?? Array.Empty<byte>(), cancellationToken);
                }
            }
            else
            {
                await System.IO.File.WriteAllBytesAsync(fullPath, fileContent ?? Array.Empty<byte>(), cancellationToken);

                await _context.Files.AddAsync(file, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Couldn't upload content.", ex);
        }

    }

    public async Task<(File, byte[])> GetFileById(Guid fileId, Guid userId, CancellationToken cancellationToken)
    {
        var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);
        if (file == null)
        {
            throw new Exception("Didnt find the file");
        }
        if (file.UserId != userId)
        {
            throw new Exception("Unauthorized access to the file.");
        }

        var fullPath = GetFullPath(file);
        await DoesFileEvenExists(file, fullPath, cancellationToken);

        var fileContent = await DownloadFile(file, fullPath, cancellationToken);
        return (file, fileContent);


    }

    public async Task<byte[]> DownloadFile(File file, string fullPath, CancellationToken cancellationToken)
    {
        
        if (!System.IO.File.Exists(fullPath))
        {
            throw new Exception("Couldn't find the file content.");
        }
        var fileContent = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        return fileContent;
    }

    public async Task<bool> SoftDeleteFileById(IReadOnlyList<Guid> fileIds, Guid userId, byte[] encryptedKey, CancellationToken cancellationToken)
    {
        if (fileIds.Count == 0) return true;

        var ids = fileIds.Distinct().ToList();

        var files = await _context.Files
            .Where(f => f.UserId == userId && !f.IsDeleted && ids.Contains(f.Id))
            .ToListAsync(cancellationToken);

        if (files.Count != ids.Count) return false;

        foreach (var file in files)
        {
            var trash = new Trash(
                id: file.Id,
                encryptedName: file.EncryptedName,
                sizeBytes: file.SizeBytes,
                deletedAt: DateTime.UtcNow,
                type: TrashType.File,
                encryptedKey: encryptedKey,
                userId: file.UserId);

            file.SoftDelete();
            await _context.Trashes.AddAsync(trash, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
    
    public async Task<List<Trash>> GetTrashedFilesAndFolder(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Trashes
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeleteFilesResult> DeleteFiles(Guid userId, List<Guid> fileIds, CancellationToken cancellationToken)
    {
        var distinctIds = fileIds.Distinct().ToList();

        if (distinctIds.Count == 0)
        {
            return DeleteFilesResult.NoFilesSelected;
        }

        var files = await _context.Files
            .Where(f => distinctIds.Contains(f.Id) && f.UserId == userId)
            .ToListAsync(cancellationToken);

        if (files.Count != distinctIds.Count)
        {
            return DeleteFilesResult.FilesNotFound;
        }

        if (files.Any(f => !f.IsDeleted))
        {
            return DeleteFilesResult.FilesNotInTrash;
        }

        foreach (var file in files)
        {
            try
            {
                System.IO.File.Delete(GetFullPath(file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not delete the content of file {FileId}. The record was kept so the operation can be retried.", file.Id);
                return DeleteFilesResult.StorageError;
            }
        }

        _context.Files.RemoveRange(files);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "The records of {Count} file(s) were changed by a parallel operation. Their content is already deleted; the operation can be retried.", files.Count);
            return DeleteFilesResult.ConcurrencyConflict;
        }

        return DeleteFilesResult.Success;
    }

    public async Task<File> DoesFileEvenExists(File file, string fullPath, CancellationToken cancellationToken)
    {
        if (System.IO.File.Exists(fullPath))
        {
            return file;
        }

        _context.Files.Remove(file);
        await _context.SaveChangesAsync(cancellationToken);

        throw new Exception("Couldn't find the file content, so the record was removed.");
    }


}
