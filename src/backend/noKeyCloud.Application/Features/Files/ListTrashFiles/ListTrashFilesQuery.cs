using MediatR;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Shared.GetFileAndFolder;

namespace noKeyCloud.Application.Features.Files.ListTrashFiles;

public record ListTrashFilesQuery(
    Guid UserId
) : IRequest<Result<GetFileAndFolderResponse>>;
