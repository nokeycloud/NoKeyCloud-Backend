using MediatR;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Trash.HardDeleteFiles;

public record HardDeleteFilesCommand(
    Guid UserId,
    IReadOnlyList<Guid> FileIds,
    IReadOnlyList<Guid> FolderIds
) : IRequest<Result>;
