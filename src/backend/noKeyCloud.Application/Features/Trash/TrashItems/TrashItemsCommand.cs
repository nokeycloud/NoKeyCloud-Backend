using MediatR;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Trash.TrashItems;

public record TrashItemsCommand(
    IReadOnlyList<Guid> FileIds,
    IReadOnlyList<Guid> FolderIds,
    Guid UserId,
    byte[] EncryptedKey
) : IRequest<Result>;
