using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Folders;

namespace noKeyCloud.Application.Features.Folders.SoftDeleteFolder;

public class SoftDeleteFolderHandler(IFolderRepository folderRepository) : IRequestHandler<SoftDeleteFolderCommand, Result<SoftDeleteFolderResponse>>
{
    public async Task<Result<SoftDeleteFolderResponse>> Handle(SoftDeleteFolderCommand request, CancellationToken cancellationToken)
    {
        List<Guid> foldersId = [];
    
        var response = await folderRepository.GetFolderAndSubFoldersById(foldersId,request.folderId ,request.userId, cancellationToken);

        if (!response.Item1) return Result<SoftDeleteFolderResponse>.Failure(response.Item2);
        
        response = await folderRepository.SoftDeleteFoldersById(foldersId ,request.userId, cancellationToken);
        
        if (!response.Item1) return Result<SoftDeleteFolderResponse>.Failure(response.Item2);
    
        var answer = new SoftDeleteFolderResponse();

        return Result<SoftDeleteFolderResponse>.Success(answer);
    }
}