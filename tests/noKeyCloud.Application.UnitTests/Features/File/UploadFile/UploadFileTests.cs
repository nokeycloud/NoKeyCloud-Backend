using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Files.UploadFile;
using noKeyCloud.Domain.Entities;
using DomainFile = noKeyCloud.Domain.Entities.File;

namespace noKeyCloud.Application.UnitTests.Features.File.UploadFile;

public class UploadFileTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly UploadFileHandler _handler;

    public UploadFileTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _folderRepositoryMock = new Mock<IFolderRepository>();

        _handler = new UploadFileHandler(
            _fileRepositoryMock.Object,
            _userRepositoryMock.Object,
            _folderRepositoryMock.Object);
    }

    private static User CreateFakeUser(Guid id) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testuser",
            registrationRecord: "reg-record",
            encryptedMasterKey: [1],
            keySalt: [2]);

    private static Folder CreateFakeFolder(Guid userId, Guid folderId)
    {
        var now = DateTime.UtcNow;
        return new Folder(
            id: folderId,
            encryptedName: [1],
            encryptedKey: [2],
            createdAt: now,
            updatedAt: now,
            parentFolderId: null,
            userId: userId);
    }

    [Fact]
    public async Task Handle_WhenUploadSucceeds_ShouldReturnSuccessWithFileId()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        var folder = CreateFakeFolder(userId, folderId);
        var content = new byte[] { 10, 20, 30 };

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _fileRepositoryMock
            .Setup(r => r.CreateFile(It.IsAny<DomainFile>(), It.IsAny<CancellationToken>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var command = new UploadFileCommand(
            userId,
            [1, 2, 3],
            "application/octet-stream",
            content.Length,
            [4, 5],
            [6, 7],
            folderId.ToString(),
            content);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.FileId);
    }

    [Fact]
    public async Task Handle_WhenUploadSucceeds_ShouldForwardFileContentToRepository()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        var folder = CreateFakeFolder(userId, folderId);
        var content = new byte[] { 100, 150, 200 };
        byte[]? capturedContent = null;

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _fileRepositoryMock
            .Setup(r => r.CreateFile(It.IsAny<DomainFile>(), It.IsAny<CancellationToken>(), It.IsAny<byte[]>()))
            .Callback<DomainFile, CancellationToken, byte[]>((_, _, c) => capturedContent = c)
            .Returns(Task.CompletedTask);

        var command = new UploadFileCommand(
            userId, [1], "application/octet-stream",
            content.Length, [2], [3],
            folderId.ToString(), content);

        await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(capturedContent);
        Assert.Equal(content, capturedContent);
    }

    [Fact]
    public async Task Handle_WhenUploadSucceeds_ShouldPersistFileSize()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        var folder = CreateFakeFolder(userId, folderId);
        const long sizeBytes = 4096L;

        DomainFile? captured = null;
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _fileRepositoryMock
            .Setup(r => r.CreateFile(It.IsAny<DomainFile>(), It.IsAny<CancellationToken>(), It.IsAny<byte[]>()))
            .Callback<DomainFile, CancellationToken, byte[]>((f, _, _) => captured = f)
            .Returns(Task.CompletedTask);

        var command = new UploadFileCommand(
            userId, [1], "application/octet-stream",
            sizeBytes, [2], [3], folderId.ToString(), [4]);

        await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(sizeBytes, captured!.SizeBytes);
    }

    [Fact]
    public async Task Handle_WhenFolderNotFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var user = CreateFakeUser(userId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder?)null);

        var command = new UploadFileCommand(
            userId, [1], "text/plain", 0, [2], [3],
            folderId.ToString(), [4]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Folder not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var folder = CreateFakeFolder(userId, folderId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var command = new UploadFileCommand(
            userId, [1], "text/plain", 0, [2], [3],
            folderId.ToString(), [4]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenFolderIdFormatInvalid_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var command = new UploadFileCommand(
            userId, [1], "text/plain", 0, [2], [3],
            "not-a-guid", [4]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Wrong id format", result.Error);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        var folder = CreateFakeFolder(userId, folderId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _fileRepositoryMock
            .Setup(r => r.CreateFile(It.IsAny<DomainFile>(), It.IsAny<CancellationToken>(), It.IsAny<byte[]>()))
            .ThrowsAsync(new Exception("Disk full"));

        var command = new UploadFileCommand(
            userId, [1], "text/plain", 0, [2], [3],
            folderId.ToString(), [4]);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Disk full", result.Error);
    }
}