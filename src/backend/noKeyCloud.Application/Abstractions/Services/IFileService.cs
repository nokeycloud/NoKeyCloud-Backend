using noKeyCloud.Application.Features.Files.DeleteFile;
using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Application.Abstractions.Services;

public interface IFileService
{
    public Task<bool> DeleteFile(DeleteFileCommand command, CancellationToken cancellationToken);
}
