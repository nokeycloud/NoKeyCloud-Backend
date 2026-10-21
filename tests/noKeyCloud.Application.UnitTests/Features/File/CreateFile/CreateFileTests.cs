using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Files.CreateFile;
using noKeyCloud.Domain.Entities;
using DomainFile = noKeyCloud.Domain.Entities.File;

namespace noKeyCloud.Application.UnitTests.Features.File.CreateFile;

public class CreateFileTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly CreateFileHandler _handler;

    public CreateFileTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _folderRepositoryMock = new Mock<IFolderRepository>();

        _handler = new CreateFileHandler(
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
            encryptedMasterKey: [1, 2, 3],
            keySalt: [4, 5, 6]);

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
    public async Task Handle_WhenFileCreated_ShouldReturnSuccessWithNonEmptyFileId()
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
            .Setup(r => r.CreateFile(
                It.IsAny<DomainFile>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var command = new CreateFileCommand(
            userId,
            [1, 2, 3],
            "text/plain",
            [4, 5],
            [6, 7],
            folderId.ToString());
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.FileId);
    }

    [Fact]
    public async Task Handle_WhenFileCreated_ShouldPersistFileWithCorrectMappedFields()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        var folder = CreateFakeFolder(userId, folderId);

        DomainFile? captured = null;
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(r => r.GetFolderByFolderId(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _fileRepositoryMock
            .Setup(r => r.CreateFile(
                It.IsAny<DomainFile>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<byte[]>()))
            .Callback<DomainFile, CancellationToken, byte[]>((f, _, _) => captured = f)
            .Returns(Task.CompletedTask);

        var encryptedName = new byte[] { 1, 2, 3 };
        var encryptedKey = new byte[] { 4, 5 };
        var checksum = new byte[] { 6, 7 };
        const string mimeType = "text/plain";

        var command = new CreateFileCommand(
            userId, encryptedName, mimeType, encryptedKey, checksum, folderId.ToString());
        
        await _handler.Handle(command, CancellationToken.None);
        
        Assert.NotNull(captured);
        Assert.Equal(encryptedName, captured!.EncryptedName);
        Assert.Equal(mimeType, captured.MimeType);
        Assert.Equal(encryptedKey, captured.EncryptedKey);
        Assert.Equal(checksum, captured.Checksum);
        Assert.Equal(folderId, captured.ParentFolderId);
        Assert.Equal(userId, captured.UserId);
        Assert.Equal(0L, captured.SizeBytes);
    }

    [Fact]
    public async Task Handle_WhenFolderNotFound_ShouldReturnFailureAndNotPersist()
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

        var command = new CreateFileCommand(
            userId, [1], "text/plain", [2], [3], folderId.ToString());
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("Folder not found", result.Error);
        _fileRepositoryMock.Verify(
            r => r.CreateFile(It.IsAny<DomainFile>(), It.IsAny<CancellationToken>(), It.IsAny<byte[]>()),
            Times.Never);
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

        var command = new CreateFileCommand(
            userId, [1], "text/plain", [2], [3], folderId.ToString());
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenFolderIdFormatIsInvalid_ShouldReturnFailureWithoutRepositoryCalls()
    {
        var userId = Guid.NewGuid();
        var command = new CreateFileCommand(
            userId, [1], "text/plain", [2], [3], "not-a-guid");
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("Wrong id format", result.Error);
        _userRepositoryMock.Verify(
            r => r.GetUserByUserId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _folderRepositoryMock.Verify(
            r => r.GetFolderByFolderId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldReturnFailureWithExceptionMessage()
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
            .ThrowsAsync(new Exception("Storage failure"));

        var command = new CreateFileCommand(
            userId, [1], "text/plain", [2], [3], folderId.ToString());
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        Assert.False(result.IsSuccess);
        Assert.Equal("Storage failure", result.Error);
    }
}