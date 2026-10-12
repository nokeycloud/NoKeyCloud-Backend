using System.Text;
using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Folders;
using noKeyCloud.Application.Features.Users.GetMe;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users;

public class GetMeQueryHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly GetMeQueryHandler _handler;

    public GetMeQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _folderRepositoryMock = new Mock<IFolderRepository>();

        _handler = new GetMeQueryHandler(
            _userRepositoryMock.Object,
            _folderRepositoryMock.Object);
    }

    private static User CreateFakeUser(Guid id, bool isAdmin = false) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testUser",
            registrationRecord: "record",
            encryptedMasterKey: [1],
            keySalt: [2],
            isAdmin: isAdmin);

    private static Folder CreateFakeRootFolder(Guid userId, Guid rootFolderId) =>
        new(
            id: rootFolderId,
            encryptedName: Encoding.UTF8.GetBytes("EncryptedFolder"),
            encryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            parentFolderId: null,
            userId: userId);

    [Fact]
    public async Task Handle_WhenUserAndFolderExist_ShouldReturnMappedResponse()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId, isAdmin: true);
        var folder = CreateFakeRootFolder(userId, rootFolderId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId.ToString(), result.Value.UserId);
        Assert.Equal("testUser", result.Value.Username);
        Assert.Equal("test@email.com", result.Value.Email);
        Assert.Equal(rootFolderId.ToString(), result.Value.RootFolderId);
        Assert.Equal(folder.EncryptedKey, result.Value.RootFolderKey);
        Assert.True(result.Value.IsAdmin);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found.", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldNotQueryFolderRepository()
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var query = new GetMeQuery(userId);

        await _handler.Handle(query, Ct);

        _folderRepositoryMock.Verify(
            x => x.GetFolderByFolderId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRootFolderMissing_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder?)null);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("Folder not found.", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotAdmin_ShouldReturnIsAdminFalse()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId, isAdmin: false);
        var folder = CreateFakeRootFolder(userId, rootFolderId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value.IsAdmin);
    }

    [Fact]
    public async Task Handle_ShouldQueryRootFolderUsingDeterministicIdFromUserId()
    {
        var userId = Guid.NewGuid();
        var expectedRootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId);
        var folder = CreateFakeRootFolder(userId, expectedRootFolderId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(expectedRootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        await _handler.Handle(query, Ct);

        _folderRepositoryMock.Verify(
            x => x.GetFolderByFolderId(expectedRootFolderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnDefaultThemeWhenNotModified()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId);
        var folder = CreateFakeRootFolder(userId, rootFolderId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("dark", result.Value.Theme);
    }

    [Fact]
    public async Task Handle_ShouldReturnUpdatedThemeWhenUserChangedIt()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId);
        user.UpdateTheme("win69");

        var folder = CreateFakeRootFolder(userId, rootFolderId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("win69", result.Value.Theme);
    }

    [Fact]
    public async Task Handle_ShouldReturnRootFolderKeyMatchingEncryptedKeyOnFolder()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId);
        var folder = CreateFakeRootFolder(userId, rootFolderId);
        var expectedKeyBytes = Encoding.UTF8.GetBytes("EncryptedKey");

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        var result = await _handler.Handle(query, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(expectedKeyBytes, result.Value.RootFolderKey);
    }

    [Fact]
    public async Task Handle_ShouldForwardCancellationTokenToBothRepositories()
    {
        var userId = Guid.NewGuid();
        var rootFolderId = FolderIdHelper.GenerateRootFolderId(userId);
        var user = CreateFakeUser(userId);
        var folder = CreateFakeRootFolder(userId, rootFolderId);

        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, Ct))
            .ReturnsAsync(user);
        _folderRepositoryMock
            .Setup(x => x.GetFolderByFolderId(rootFolderId, Ct))
            .ReturnsAsync(folder);

        var query = new GetMeQuery(userId);

        await _handler.Handle(query, Ct);

        _userRepositoryMock.Verify(x => x.GetUserByUserId(userId, Ct), Times.Once);
        _folderRepositoryMock.Verify(x => x.GetFolderByFolderId(rootFolderId, Ct), Times.Once);
    }
}