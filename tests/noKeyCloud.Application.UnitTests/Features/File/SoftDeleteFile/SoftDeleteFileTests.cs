using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Shared;

namespace noKeyCloud.Application.UnitTests.Features.File.SoftDeleteFile;

public class SoftDeleteFileTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly SoftDeleteFileHandler _handler;

    public SoftDeleteFileTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();
        _folderRepositoryMock = new Mock<IFolderRepository>();

        _handler = new SoftDeleteFileHandler(
            _fileRepositoryMock.Object,
            _folderRepositoryMock.Object);
    }

    private void SetupFiles(bool result)
    {
        _fileRepositoryMock
            .Setup(r => r.SoftDeleteById(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupFolderLookup(bool success, string error = "")
    {
        _folderRepositoryMock
            .Setup(r => r.GetFolderAndSubFoldersById(
                It.IsAny<List<Guid>>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((success, error));
    }

    private void SetupFolderDelete(bool success, string error = "")
    {
        _folderRepositoryMock
            .Setup(r => r.SoftDeleteFoldersById(
                It.IsAny<List<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((success, error));
    }

    [Fact]
    public async Task Handle_WithFilesAndFolders_ShouldSoftDeleteBoth()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var folderId = Guid.NewGuid();

        SetupFolderLookup(true);
        SetupFiles(true);
        SetupFolderDelete(true);

        var command = new SoftDeleteFileCommand([fileId], [folderId], userId, [1, 2, 3]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _fileRepositoryMock.Verify(r => r.SoftDeleteById(
            It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(fileId)),
            It.IsAny<IReadOnlyList<Guid>>(),
            userId,
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _folderRepositoryMock.Verify(r => r.GetFolderAndSubFoldersById(
            It.IsAny<List<Guid>>(),
            It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(folderId)),
            userId,
            It.IsAny<CancellationToken>()), Times.Once);

        _folderRepositoryMock.Verify(r => r.SoftDeleteFoldersById(
            It.IsAny<List<Guid>>(),
            userId,
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithFilesOnly_ShouldNotTouchFolders()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        SetupFiles(true);

        var command = new SoftDeleteFileCommand([fileId], [], userId, [1]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _fileRepositoryMock.Verify(r => r.SoftDeleteById(
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<IReadOnlyList<Guid>>(),
            userId,
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _folderRepositoryMock.Verify(r => r.GetFolderAndSubFoldersById(
            It.IsAny<List<Guid>>(),
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _folderRepositoryMock.Verify(r => r.SoftDeleteFoldersById(
            It.IsAny<List<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFoldersOnly_ShouldNotTouchFiles()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();

        SetupFolderLookup(true);
        SetupFolderDelete(true);

        var command = new SoftDeleteFileCommand([], [folderId], userId, [1]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _fileRepositoryMock.Verify(r => r.SoftDeleteById(
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _folderRepositoryMock.Verify(r => r.SoftDeleteFoldersById(
            It.IsAny<List<Guid>>(),
            userId,
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoIds_ShouldReturnFailure()
    {
        var command = new SoftDeleteFileCommand([], [], Guid.NewGuid(), [1]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No file or folder ids provided", result.Error);

        _fileRepositoryMock.Verify(r => r.SoftDeleteById(
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();

        SetupFiles(false);

        var command = new SoftDeleteFileCommand([Guid.NewGuid()], [], userId, [1]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not find file", result.Error);
    }

    [Fact]
    public async Task Handle_WhenFolderIsInvalid_ShouldFailBeforeTouchingFiles()
    {
        var userId = Guid.NewGuid();

        SetupFolderLookup(false, "You can not delete root folder");
        SetupFiles(true);

        var command = new SoftDeleteFileCommand([Guid.NewGuid()], [Guid.NewGuid()], userId, [1]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You can not delete root folder", result.Error);

        _fileRepositoryMock.Verify(r => r.SoftDeleteById(
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

