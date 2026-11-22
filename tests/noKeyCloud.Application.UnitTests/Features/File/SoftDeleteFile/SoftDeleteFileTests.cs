using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Files.SoftDeleteFile;

namespace noKeyCloud.Application.UnitTests.Features.File.SoftDeleteFile;

public class SoftDeleteFileTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly SoftDeleteFileHandler _handler;

    public SoftDeleteFileTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new SoftDeleteFileHandler(
            _fileRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSoftDeletedFile_ShouldReturnSuccess()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        
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
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task Handle_WhenUserIdOrFileIdIsInvalid_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        
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
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}