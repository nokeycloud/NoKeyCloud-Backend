using MediatR;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Shared.Delete;

namespace noKeyCloud.Application.Features.Shared;

public record SoftDeleteFileCommand(
    IReadOnlyList<Guid> FileIds,
    IReadOnlyList<Guid> FolderIds,
    Guid userId,
    byte[] encryptedKey

) : IRequest<Result<FolderAndFileResponse>>;
