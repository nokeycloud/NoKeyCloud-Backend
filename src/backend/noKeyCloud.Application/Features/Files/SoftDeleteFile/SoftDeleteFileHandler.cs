using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.File;

namespace noKeyCloud.Application.Features.Files.SoftDeleteFile;

public class SoftDeleteFileHandler(IFileRepository fileRepository, IUnitOfWork unitOfWork) : IRequestHandler<SoftDeleteFileCommand, Result<SoftDeleteFileResponse>>
{
    public async Task<Result<SoftDeleteFileResponse>> Handle(SoftDeleteFileCommand request, CancellationToken cancellationToken)
    {
        var response = await fileRepository.SoftDeleteFileById(request.fileId, request.userId, cancellationToken);

        if (!response) return Result<SoftDeleteFileResponse>.Failure("Could not find file");
        
        await unitOfWork.SaveChangesAsync(cancellationToken);
        
        var answer = new SoftDeleteFileResponse();

        return Result<SoftDeleteFileResponse>.Success(answer);
    }
}

 