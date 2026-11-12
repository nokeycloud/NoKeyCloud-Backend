using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Features.Users.RemoveUser;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users.RemoveUser;

public class RemoveUserTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenProvider> _refreshTokenProviderMock;
    private readonly RemoveUserCommandHandler _handler;

    public RemoveUserTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenProviderMock = new Mock<IRefreshTokenProvider>();

        _handler = new RemoveUserCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenProviderMock.Object);
    }

    private static User CreateFakeUser(Guid id) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "test_user",
            registrationRecord: "record",
            encryptedMasterKey: [1],
            keySalt: [2]);

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnFailure()
    {
        var command = new RemoveUserCommand(Guid.NewGuid());
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(command, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldNotAttemptRemoval()
    {
        var command = new RemoveUserCommand(Guid.NewGuid());
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await _handler.Handle(command, Ct);

        _userRepositoryMock.Verify(
            x => x.RemoveUserByUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldNotRevokeSessions()
    {
        var command = new RemoveUserCommand(Guid.NewGuid());
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await _handler.Handle(command, Ct);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeAllSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsRemoved_ShouldReturnSuccessMessage()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(x => x.RemoveUserByUser(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.Handle(new RemoveUserCommand(userId), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("User successfully removed", result.Value!.Message);
    }

    [Fact]
    public async Task Handle_WhenUserIsRemoved_ShouldRevokeAllSessions()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(x => x.RemoveUserByUser(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _handler.Handle(new RemoveUserCommand(userId), Ct);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeAllSessionsAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryRemovalFails_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(x => x.RemoveUserByUser(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("FK constraint"));

        var result = await _handler.Handle(new RemoveUserCommand(userId), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("FK constraint", result.Error);
    }

    [Fact]
    public async Task Handle_WhenRepositoryRemovalFails_ShouldNotRevokeSessions()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(x => x.RemoveUserByUser(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("FK constraint"));

        await _handler.Handle(new RemoveUserCommand(userId), Ct);

        _refreshTokenProviderMock.Verify(
            x => x.RevokeAllSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldForwardCancellationTokenToAllCollaborators()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);
        _userRepositoryMock
            .Setup(x => x.GetUserByUserId(userId, Ct))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(x => x.RemoveUserByUser(user, Ct))
            .ReturnsAsync(Result.Success());
        _refreshTokenProviderMock
            .Setup(x => x.RevokeAllSessionsAsync(userId, Ct))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new RemoveUserCommand(userId), Ct);

        _userRepositoryMock.Verify(x => x.GetUserByUserId(userId, Ct), Times.Once);
        _userRepositoryMock.Verify(x => x.RemoveUserByUser(user, Ct), Times.Once);
        _refreshTokenProviderMock.Verify(x => x.RevokeAllSessionsAsync(userId, Ct), Times.Once);
    }
}