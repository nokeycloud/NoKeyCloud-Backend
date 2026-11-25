using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Trash;

namespace noKeyCloud.Application.Features.Trash.ListTrash;

public class ListTrashHandler(ITrashRepository trashRepository)
    : IRequestHandler<ListTrashQuery, Result<GetTrashResponse>>
{
    public async Task<Result<GetTrashResponse>> Handle(
        ListTrashQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var trashes = await trashRepository.GetTrashItems(request.UserId, cancellationToken);

            var response = new GetTrashResponse
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

            return Result<GetTrashResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<GetTrashResponse>.Failure(ex.Message);
        }
    }
}
