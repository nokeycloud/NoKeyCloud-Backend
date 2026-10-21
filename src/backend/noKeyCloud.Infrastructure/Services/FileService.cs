using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Files.DeleteFile;
using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Infrastructure.Services;

public class FileService(IFileRepository fileRepository) : IFileService
{
    public async Task<bool> DeleteFile(DeleteFileCommand command, CancellationToken cancellationToken)
    {
        var listLength = command.FileListIds.Count;
        if (listLength == 0)
        {
            throw new Exception("There is nothing to delete");
        }else
        {
            for (int i = 0; i < listLength; i++)
            {
                var fileId = command.FileListIds[i];
                var result = await fileRepository.DeleteFile(command.UserId,fileId, cancellationToken);
                if(result == false)
                {
                    throw new Exception("Coudln't delete the file");
                }

                
            }
        }
        return true;
    }
}
