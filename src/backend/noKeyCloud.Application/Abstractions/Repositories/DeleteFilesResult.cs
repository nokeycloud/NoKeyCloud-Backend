namespace noKeyCloud.Application.Abstractions.Repositories;

public enum DeleteFilesResult
{
    Success,
    NoFilesSelected,
    FilesNotFound,
    FilesNotInTrash,
    StorageError,
    ConcurrencyConflict
}
