using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.File;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace noKeyCloud.Application.Features.Files.DeleteFile;

public class DeleteFileCommandHandler(IFileRepository fileRepository) : IRequestHandler<DeleteFileCommand, Result<DeleteFileResponse>>
{
    public async Task<Result<DeleteFileResponse>> Handle(DeleteFileCommand command, CancellationToken cancellationToken)
    {
        var fileIds = command.FileListIds.Distinct().ToList();
        if (fileIds.Count == 0)
        {
            return Result<DeleteFileResponse>.Success(new DeleteFileResponse { Success = true });
        }

        var deleted = await fileRepository.DeleteFiles(command.UserId, fileIds, cancellationToken);
        if (!deleted)
        {
            return Result<DeleteFileResponse>.Failure("Couldn't delete the files. Make sure they exist and belong to you.");
        }

        return Result<DeleteFileResponse>.Success(new DeleteFileResponse { Success = true });
    }
}
