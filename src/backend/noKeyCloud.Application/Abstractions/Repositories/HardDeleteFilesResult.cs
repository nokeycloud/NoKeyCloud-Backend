namespace noKeyCloud.Application.Abstractions.Repositories;

public enum HardDeleteFilesResult
{
    Success,
    NoFilesSelected,
    FilesNotFound,
    FilesNotInTrash,
    StorageError,
    ConcurrencyConflict
}
