using MediatR;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.File;

namespace noKeyCloud.Application.Features.Files.SoftDeleteFile;

public record SoftDeleteFileCommand(
    Guid userId,
    Guid fileId

) : IRequest<Result<SoftDeleteFileResponse>>;