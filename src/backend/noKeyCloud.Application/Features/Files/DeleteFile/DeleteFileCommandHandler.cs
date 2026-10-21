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

public class DeleteFileCommandHandler(IFileService fileService) : IRequestHandler<DeleteFileCommand, Result<DeleteFileResponse>>
{
    public async Task<Result<DeleteFileResponse>> Handle(DeleteFileCommand command, CancellationToken cancellationToken)
    {
        var result = await fileService.DeleteFile(command, cancellationToken);
        var response = new DeleteFileResponse
        {
            Success = result
        };
        return Result<DeleteFileResponse>.Success(response);
        
    }
}

