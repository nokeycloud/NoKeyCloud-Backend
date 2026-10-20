using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Users.UpdateTheme;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users.UpdateTheme;

public class UpdateThemeCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly UpdateThemeCommandHandler _handler;

    public UpdateThemeCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new UpdateThemeCommandHandler(_userRepositoryMock.Object);
    }

    private static User CreateFakeUser(Guid id) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testuser",
            registrationRecord: "record",
            encryptedMasterKey: [1],
            keySalt: [2]);

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(
            new UpdateThemeCommand(userId, "dark"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldNotPersist()
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await _handler.Handle(new UpdateThemeCommand(userId, "dark"), CancellationToken.None);

        _userRepositoryMock.Verify(
            r => r.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldUpdateThemeAndReturnSuccess()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(r => r.UpdateUserAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new UpdateThemeCommand(userId, "win69"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("win69", user.Theme);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldPersistUpdatedUser()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(r => r.UpdateUserAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new UpdateThemeCommand(userId, "light"), CancellationToken.None);

        _userRepositoryMock.Verify(
            r => r.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("win69")]
    public async Task Handle_ShouldApplyProvidedThemeVerbatim(string theme)
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(r => r.UpdateUserAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new UpdateThemeCommand(userId, theme), CancellationToken.None);

        Assert.Equal(theme, user.Theme);
    }
}