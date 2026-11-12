using MediatR;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Folders;

namespace noKeyCloud.Application.Features.Folders.SoftDeleteFolder;

public record SoftDeleteFolderCommand(
    Guid userId,
    Guid folderId

) : IRequest<Result<SoftDeleteFolderResponse>>;