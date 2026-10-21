using MediatR;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.File;
using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Application.Features.Files.DeleteFile;

public record DeleteFileCommand : IRequest<Result<DeleteFileResponse>>
{
    public required Guid UserId { get; init; }
    public required List<Guid> FileListIds { get; init; }
};
