using MediatR;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Trash;

namespace noKeyCloud.Application.Features.Trash.ListTrash;

public record ListTrashQuery(Guid UserId) : IRequest<Result<GetTrashResponse>>;
