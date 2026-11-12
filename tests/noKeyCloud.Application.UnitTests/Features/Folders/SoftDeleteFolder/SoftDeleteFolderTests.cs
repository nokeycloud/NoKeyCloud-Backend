using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Folders.SoftDeleteFolder;

namespace noKeyCloud.Application.UnitTests.Features.Folders.SoftDeleteFolder;

public class SoftDeleteFolderTests
{
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly SoftDeleteFolderHandler _handler;

    public SoftDeleteFolderTests()
    {
        _folderRepositoryMock = new Mock<IFolderRepository>();

        _handler = new SoftDeleteFolderHandler(_folderRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSoftDeletedFolder_ShouldReturnSuccess()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        
        _folderRepositoryMock
            .Setup(r => r.GetFolderAndSubFoldersById(
                It.IsAny<List<Guid>>(),
                folderId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true,""));
        
        _folderRepositoryMock
            .Setup(r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true,""));
        
        var command = new SoftDeleteFolderCommand(
            userId,
            folderId);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
        
        _folderRepositoryMock
            .Verify(r => r.GetFolderAndSubFoldersById(
                It.IsAny<List<Guid>>(),
                folderId,
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        
        _folderRepositoryMock
            .Verify(r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
                Times.Once);
    }
    
    [Fact]
    public async Task Handle_WhenUserIdOrFolderIdIsInvalid_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        
        _folderRepositoryMock
            .Setup(r => r.GetFolderAndSubFoldersById(
                It.IsAny<List<Guid>>(),
                folderId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false,"Could not find folder"));
        
        var command = new SoftDeleteFolderCommand(
            userId,
            folderId);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("Could not find folder", result.Error);
        
        _folderRepositoryMock
            .Verify(r => r.GetFolderAndSubFoldersById(
                    It.IsAny<List<Guid>>(),
                    folderId,
                    userId,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        
        _folderRepositoryMock.Verify(
            r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task Handle_WhenUserTryToDeleteRootFolder_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        
        _folderRepositoryMock
            .Setup(r => r.GetFolderAndSubFoldersById(
                It.IsAny<List<Guid>>(),
                folderId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false,"You can not delete root folder"));
        
        var command = new SoftDeleteFolderCommand(
            userId,
            folderId);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("You can not delete root folder", result.Error);
        
        _folderRepositoryMock
            .Verify(r => r.GetFolderAndSubFoldersById(
                    It.IsAny<List<Guid>>(),
                    folderId,
                    userId,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        
        _folderRepositoryMock.Verify(
            r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task Handle_WhenNotAllFoldersWereFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        
        _folderRepositoryMock
            .Setup(r => r.GetFolderAndSubFoldersById(
                It.IsAny<List<Guid>>(),
                folderId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true,""));
        
        _folderRepositoryMock
            .Setup(r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false,"Not all folders were found"));
        
        var command = new SoftDeleteFolderCommand(
            userId,
            folderId);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("Not all folders were found", result.Error);
        
        _folderRepositoryMock
            .Verify(r => r.GetFolderAndSubFoldersById(
                    It.IsAny<List<Guid>>(),
                    folderId,
                    userId,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        
        _folderRepositoryMock.Verify(
            r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}