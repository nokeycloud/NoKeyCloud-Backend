using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Shared.Delete;

namespace noKeyCloud.Application.Features.Shared;

public class SoftDeleteFileHandler(IFileRepository fileRepository, IFolderRepository folderRepository)
    : IRequestHandler<SoftDeleteFileCommand, Result<FolderAndFileResponse>>
{
    public async Task<Result<FolderAndFileResponse>> Handle(SoftDeleteFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FileIds.Count == 0 && request.FolderIds.Count == 0)
        {
            return Result<FolderAndFileResponse>.Failure("No file or folder ids provided");
        }

        List<Guid> foldersId = [];

        if (request.FolderIds.Count > 0)
        {
            var foldersResponse = await folderRepository.GetFolderAndSubFoldersById(
                foldersId, request.FolderIds, request.userId, cancellationToken);

            if (!foldersResponse.Item1)
                return Result<FolderAndFileResponse>.Failure(foldersResponse.Item2);
        }

        if (request.FileIds.Count > 0)
        {
            var filesDeleted = await fileRepository.SoftDeleteFileById(
                request.FileIds, request.userId, request.encryptedKey, cancellationToken);

            if (!filesDeleted)
                return Result<FolderAndFileResponse>.Failure("Could not find file");
        }

        if (foldersId.Count > 0)
        {
            var foldersResponse = await folderRepository.SoftDeleteFoldersById(
                foldersId, request.userId, request.encryptedKey, cancellationToken);

            if (!foldersResponse.Item1)
                return Result<FolderAndFileResponse>.Failure(foldersResponse.Item2);
        }

        return Result<FolderAndFileResponse>.Success(new FolderAndFileResponse());
    }
}

