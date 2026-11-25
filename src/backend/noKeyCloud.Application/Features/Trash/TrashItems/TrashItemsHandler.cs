using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Trash.TrashItems;

public class TrashItemsHandler(IFileRepository fileRepository, IFolderRepository folderRepository)
    : IRequestHandler<TrashItemsCommand, Result>
{
    public async Task<Result> Handle(TrashItemsCommand request, CancellationToken cancellationToken)
    {
        if (request.FileIds.Count == 0 && request.FolderIds.Count == 0)
        {
            return Result.Failure("No file or folder ids provided");
        }

        List<Guid> foldersId = [];

        if (request.FolderIds.Count > 0)
        {
            var foldersResponse = await folderRepository.GetFolderAndSubFoldersById(
                foldersId, request.FolderIds, request.UserId, cancellationToken);

            if (!foldersResponse.Item1)
                return Result.Failure(foldersResponse.Item2);
        }

        if (request.FileIds.Count > 0)
        {
            var filesDeleted = await fileRepository.SoftDeleteFileById(
                request.FileIds, request.UserId, request.EncryptedKey, cancellationToken);

            if (!filesDeleted)
                return Result.Failure("Could not find file");
        }

        if (foldersId.Count > 0)
        {
            var foldersResponse = await folderRepository.SoftDeleteFoldersById(
                foldersId, request.UserId, request.EncryptedKey, cancellationToken);

            if (!foldersResponse.Item1)
                return Result.Failure(foldersResponse.Item2);
        }

        return Result.Success();
    }
}
