using Microsoft.EntityFrameworkCore;
using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Folders.CreateFolder;
using noKeyCloud.Domain.Entities;
using System.Text;

namespace noKeyCloud.Application.UnitTests.Features.Folders.Commands;

public class CreateFolderHandlerTests
{
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateFolderHandler _handler;

    public CreateFolderHandlerTests()
    {
        _folderRepositoryMock = new Mock<IFolderRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateFolderHandler(_folderRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateFolder_AndReturnIdAndName()
    {
        var userId = Guid.NewGuid();
        var command = new CreateFolderCommand(
            UserId: userId,
            EncryptedName: Encoding.UTF8.GetBytes("EncryptedFolderName"),
            EncryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            ParentFolderId: Guid.NewGuid()
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);

        _folderRepositoryMock.Verify(
            x => x.Add(
                It.Is<Folder>(f =>
                    f.UserId == command.UserId &&
                    Enumerable.SequenceEqual(f.EncryptedName, command.EncryptedName) &&
                    Enumerable.SequenceEqual(f.EncryptedKey, command.EncryptedKey) &&
                    f.ParentFolderId == command.ParentFolderId)),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithProvidedParentFolderId_ShouldMapParentFolderIdCorrectly()
    {
        var parentId = Guid.NewGuid();
        var command = new CreateFolderCommand(
            UserId: Guid.NewGuid(),
            EncryptedName: Encoding.UTF8.GetBytes("EncryptedFolderName"),
            EncryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            ParentFolderId: parentId
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        _folderRepositoryMock.Verify(
            x => x.Add(It.Is<Folder>(f => f.ParentFolderId == parentId)),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithGuidEmptyParentFolderId_ShouldThrowArgumentException()
    {
        var command = new CreateFolderCommand(
            UserId: Guid.NewGuid(),
            EncryptedName: Encoding.UTF8.GetBytes("EncryptedFolderName"),
            EncryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            ParentFolderId: Guid.Empty
        );

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("cannot be Guid.Empty", exception.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrowsDbUpdateException_ShouldPropagateException()
    {
        var command = new CreateFolderCommand(
            UserId: Guid.NewGuid(),
            EncryptedName: Encoding.UTF8.GetBytes("EncryptedFolderName"),
            EncryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            ParentFolderId: Guid.NewGuid()
        );

        var innerException = new Exception("Database constraint violation");
        var dbException = new DbUpdateException("An error occurred while updating the entries.", innerException);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbException);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(act);
        Assert.Equal(dbException.Message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public async Task Handle_WhenCancellationTokenIsCancelled_ShouldThrowOperationCanceledException()
    {
        var command = new CreateFolderCommand(
            UserId: Guid.NewGuid(),
            EncryptedName: Encoding.UTF8.GetBytes("EncryptedFolderName"),
            EncryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            ParentFolderId: Guid.NewGuid()
        );

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationTokenSource.Token));

        var act = async () => await _handler.Handle(command, cancellationTokenSource.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }
    
    [Fact]
    public async Task Handle_WithNullParentFolderId_ShouldThrowException_SoASecondRootFolderCannotBeCreated()
    {
        var command = new CreateFolderCommand(
            UserId: Guid.NewGuid(),
            EncryptedName: Encoding.UTF8.GetBytes("EncryptedFolderName"),
            EncryptedKey: Encoding.UTF8.GetBytes("EncryptedKey"),
            ParentFolderId: null
        );
        
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        
        var exception = await Assert.ThrowsAsync<Exception>(act);
        Assert.Equal("ParentFolderId cannot be null.", exception.Message);
        
        _folderRepositoryMock.Verify(x => x.Add(It.IsAny<Folder>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}