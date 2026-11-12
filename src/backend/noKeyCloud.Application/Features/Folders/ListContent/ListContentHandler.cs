using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Contracts.Folders;

namespace noKeyCloud.Application.Features.Folders.ListContent
{
    public class ListContentHandler : IRequestHandler<ListContentQuery, Result<ListContentResponse>>
    {
        private readonly IFolderRepository _FolderRepository;
        public ListContentHandler(IFolderRepository folderRepository)
        {
            _FolderRepository = folderRepository;
        }
        public async Task<Result<ListContentResponse>> Handle(ListContentQuery request, CancellationToken cancellationToken)
        {

            try
            {
                var requestedFolder = await _FolderRepository.GetFolderByFolderId(request.FolderId, cancellationToken);
                
                if (requestedFolder == null) return Result<ListContentResponse>.Failure("Folder not found");
                
                if(requestedFolder.IsDeleted) return Result<ListContentResponse>.Failure("Folder is deleted");
                
                var folder =
                    await _FolderRepository.GetFilesOrContentAsync(request.FolderId, request.UserId, cancellationToken);

                var files = folder.Item1.Where(f => !f.IsDeleted).ToList();
                
                var folders = folder.Item2.Where(f => !f.IsDeleted).ToList();

                var response = new ListContentResponse
                (
                    Files: files.Select(f => new FileResponse
                    (
                        FilesId: f.Id,
                        FileNameEncrypted: f.EncryptedName,
                        MimeType: f.MimeType,
                        SizeBytes: f.SizeBytes,
                        FileKeyEncrypted: f.EncryptedKey,
                        CheckSum: f.Checksum,
                        CreatedAt: f.CreatedAt,
                        UpdatedAt: f.UpdatedAt
                    )).ToList(),

                    Folders: folders.Select(f => new FolderResponse
                    (
                        FolderId: f.Id,
                        NameEncrypted: f.EncryptedName,
                        FolderKeyEncrypted: f.EncryptedKey,
                        CreatedAt: f.CreatedAt,
                        UpdatedAt: f.UpdatedAt,
                        ParentFolderId: f.ParentFolderId
                    )).ToList()
                );

                return Result<ListContentResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return Result<ListContentResponse>.Failure(ex.Message);
            }
            
        }
    }
}