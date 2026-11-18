using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.File;

namespace noKeyCloud.Application.Features.Files.HardDeleteFile;

public class DeleteFileCommandHandler(IFileRepository fileRepository) : IRequestHandler<DeleteFileCommand, Result<DeleteFileResponse>>
{
    public async Task<Result<DeleteFileResponse>> Handle(DeleteFileCommand command, CancellationToken cancellationToken)
    {
        var result = await fileRepository.DeleteFiles(command.UserId, command.FileListIds, cancellationToken);

        return result switch
        {
            DeleteFilesResult.Success => Result<DeleteFileResponse>.Success(new DeleteFileResponse { Success = true }),
            DeleteFilesResult.NoFilesSelected => Result<DeleteFileResponse>.Failure("No files selected."),
            DeleteFilesResult.FilesNotFound => Result<DeleteFileResponse>.Failure("Some files do not exist or do not belong to you."),
            DeleteFilesResult.FilesNotInTrash => Result<DeleteFileResponse>.Failure("Files must be in the trash before they can be deleted permanently."),
            DeleteFilesResult.StorageError => Result<DeleteFileResponse>.Failure("The file content could not be deleted. Try again later."),
            DeleteFilesResult.ConcurrencyConflict => Result<DeleteFileResponse>.Failure("Files were changed by another operation. Refresh and try again."),
            _ => Result<DeleteFileResponse>.Failure("The files could not be deleted.")
        };
    }
}