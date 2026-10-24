using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Files.CreateFile;
using noKeyCloud.Application.Features.Files.SoftDeleteFile;
using noKeyCloud.Domain.Entities;


namespace noKeyCloud.Application.UnitTests.Features.File.SoftDeleteFile;

public class SoftDeleteFileTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly SoftDeleteFileHandler _handler;

    public SoftDeleteFileTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();

        _handler = new SoftDeleteFileHandler(
            _fileRepositoryMock.Object);
    }
    
    private static Domain.Entities.File CreateFakeFile(Guid userId, Guid fileId)
    {
        var now = DateTime.UtcNow;
        return new Domain.Entities.File(
            id: fileId,
            encryptedName: [1],
            mimeType: "text/plain",
            sizeBytes: 12341,
            storagePath: "path/to/file",
            encryptedKey: [2],
            checksum: [1],
            parentFolderId: Guid.NewGuid(),
            userId: userId);
    }

    [Fact]
    public async Task Handle_WhenSoftDeletedFile_ShouldReturnSuccess()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var file = CreateFakeFile(userId, fileId);
        
        _fileRepositoryMock
            .Setup(r => r.SoftDeleteFileById(
                fileId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new SoftDeleteFileCommand(
            userId,
            fileId);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
    }
    
    [Fact]
    public async Task Handle_WhenUserIdOrFileIdIsInvalid_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var file = CreateFakeFile(userId, fileId);
        
        _fileRepositoryMock
            .Setup(r => r.SoftDeleteFileById(
                fileId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new SoftDeleteFileCommand(
            userId,
            fileId);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("Could not find file", result.Error);
    }
}