using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Trash.HardDeleteFiles;

public class HardDeleteFilesCommandHandler(ITrashRepository trashRepository)
    : IRequestHandler<HardDeleteFilesCommand, Result>
{
    public async Task<Result> Handle(HardDeleteFilesCommand command, CancellationToken cancellationToken)
    {
        var result = await trashRepository.HardDeleteFiles(command.UserId, command.FileIds, cancellationToken);

        return result switch
        {
            HardDeleteFilesResult.Success => Result.Success(),
            HardDeleteFilesResult.NoFilesSelected => Result.Failure("No files selected."),
            HardDeleteFilesResult.FilesNotFound => Result.Failure("Some files do not exist or do not belong to you."),
            HardDeleteFilesResult.FilesNotInTrash => Result.Failure("Files must be in the trash before they can be deleted permanently."),
            HardDeleteFilesResult.StorageError => Result.Failure("The file content could not be deleted. Try again later."),
            HardDeleteFilesResult.ConcurrencyConflict => Result.Failure("Files were changed by another operation. Refresh and try again."),
            _ => Result.Failure("The files could not be deleted.")
        };
    }
}
