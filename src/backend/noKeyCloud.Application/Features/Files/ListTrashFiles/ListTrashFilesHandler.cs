using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Shared.GetFileAndFolder;

namespace noKeyCloud.Application.Features.Files.ListTrashFiles;

public class ListTrashFilesHandler(IFileRepository fileRepository)
    : IRequestHandler<ListTrashFilesQuery, Result<GetFileAndFolderResponse>>
{
    public async Task<Result<GetFileAndFolderResponse>> Handle(
        ListTrashFilesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var trashes = await fileRepository.GetTrashedFilesAndFolder(request.UserId, cancellationToken);

            var response = new GetFileAndFolderResponse
            {
                Trashes = trashes.Select(t => new TrashItem(
                    t.Id,
                    t.EncryptedName,
                    t.SizeBytes,
                    t.Type,
                    t.EncryptedKey,
                    t.DeletedAt,
                    t.UserId)).ToList()
            };

            return Result<GetFileAndFolderResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<GetFileAndFolderResponse>.Failure(ex.Message);
        }
    }
}
