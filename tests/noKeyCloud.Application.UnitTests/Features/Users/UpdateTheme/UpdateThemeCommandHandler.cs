using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Users.UpdateTheme;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.UnitTests.Features.Users.UpdateTheme;

public class UpdateThemeCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateThemeCommandHandler _handler;

    public UpdateThemeCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new UpdateThemeCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    private static User CreateFakeUser(Guid id) =>
        new(
            id: id,
            userIdentifier: Guid.NewGuid(),
            email: "test@email.com",
            username: "testuser");

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

        _userRepositoryMock.Verify(r => r.UpdateUser(It.IsAny<User>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldUpdateThemeAndReturnSuccess()
    {
        var userId = Guid.NewGuid();
        var user = CreateFakeUser(userId);

        _userRepositoryMock
            .Setup(r => r.GetUserByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

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

        await _handler.Handle(new UpdateThemeCommand(userId, "light"), CancellationToken.None);

        _userRepositoryMock.Verify(r => r.UpdateUser(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        await _handler.Handle(new UpdateThemeCommand(userId, theme), CancellationToken.None);

        Assert.Equal(theme, user.Theme);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}