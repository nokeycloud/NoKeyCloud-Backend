using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Trash;
using noKeyCloud.Domain.Entities;
using File = noKeyCloud.Domain.Entities.File;

namespace noKeyCloud.Infrastructure.Repositories;

public class TrashRepository : ITrashRepository
{
    private const string FileExtension = ".nkc";
    private readonly DataContext _context;
    private readonly ILogger<TrashRepository> _logger;
    private readonly string _baseStoragePath;

    public TrashRepository(DataContext context, ILogger<TrashRepository> logger)
    {
        _context = context;
        _logger = logger;

        var envPath = Environment.GetEnvironmentVariable("BASE_UPLOAD_STORAGE_PATH");
        if (string.IsNullOrWhiteSpace(envPath))
        {
            throw new InvalidOperationException("Required environment variable 'BASE_UPLOAD_STORAGE_PATH' is missing or empty. Configure 'BASE_UPLOAD_STORAGE_PATH' before starting the application.");
        }
        _baseStoragePath = envPath;
    }

    private string GetFullPath(File file)
    {
        return Path.Combine(_baseStoragePath, $"{file.Id}{FileExtension}");
    }

    public async Task<List<Trash>> GetTrashItems(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Trashes
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<HardDeleteFilesResult> HardDeleteFiles(
        Guid userId, IReadOnlyList<Guid> fileIds, CancellationToken cancellationToken)
    {
        var distinctIds = fileIds.Distinct().ToList();

        if (distinctIds.Count == 0)
        {
            return HardDeleteFilesResult.NoFilesSelected;
        }

        var files = await _context.Files
            .Where(f => distinctIds.Contains(f.Id) && f.UserId == userId)
            .ToListAsync(cancellationToken);

        if (files.Count != distinctIds.Count)
        {
            return HardDeleteFilesResult.FilesNotFound;
        }

        if (files.Any(f => !f.IsDeleted))
        {
            return HardDeleteFilesResult.FilesNotInTrash;
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
                return HardDeleteFilesResult.StorageError;
            }
        }

        var trashes = await _context.Trashes
            .Where(t => t.UserId == userId && t.Type == TrashType.File && distinctIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        _context.Trashes.RemoveRange(trashes);
        _context.Files.RemoveRange(files);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "The records of {Count} file(s) were changed by a parallel operation. Their content is already deleted; the operation can be retried.", files.Count);
            return HardDeleteFilesResult.ConcurrencyConflict;
        }

        return HardDeleteFilesResult.Success;
    }
}
