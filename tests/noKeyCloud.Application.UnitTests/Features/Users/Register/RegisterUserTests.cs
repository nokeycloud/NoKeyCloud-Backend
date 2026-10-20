using Moq;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Features.Folders;
using noKeyCloud.Application.Features.Users.Register;
using noKeyCloud.Domain.Entities;
using noKeyCloud.Domain.Enums;

namespace noKeyCloud.Application.UnitTests.Features.Users.Register;

public class RegisterUserTests
{
    private static readonly byte[] EncryptedMasterKey = [1];
    private static readonly byte[] KeySalt = [2];
    private static readonly byte[] RecoveryEncryptedMasterKey = [3];
    private static readonly byte[] RootFolderKey = [4];

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IFolderRepository> _folderRepositoryMock;
    private readonly Mock<IRegisterInviteRepository> _registerInviteRepositoryMock;
    private readonly Mock<IRecoveryMethodRepository> _recoveryMethodRepositoryMock;
    private readonly RegisterUserHandler _handler;

    public RegisterUserTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _folderRepositoryMock = new Mock<IFolderRepository>();
        _registerInviteRepositoryMock = new Mock<IRegisterInviteRepository>();
        _recoveryMethodRepositoryMock = new Mock<IRecoveryMethodRepository>();

        _handler = new RegisterUserHandler(
            _userRepositoryMock.Object,
            _folderRepositoryMock.Object,
            _registerInviteRepositoryMock.Object,
            _recoveryMethodRepositoryMock.Object);
    }

    private static RegisterUserCommand BuildCommand(
        string username = "minipaka",
        string email = "minipaka@test.com",
        string? inviteCode = null) =>
        new(
            UserIdentifier: Guid.NewGuid(),
            Username: username,
            Email: email,
            RegistrationRecord: "opaque-record",
            EncryptedMasterKey: EncryptedMasterKey,
            KeySalt: KeySalt,
            RecoveryEncryptedMasterKey: RecoveryEncryptedMasterKey,
            RootFolderKey: RootFolderKey,
            RegisterInviteCode: inviteCode);

    private void SetupFirstUserPersist()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_FirstUser_ShouldRegisterAsAdmin()
    {
        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedUser);
        Assert.True(capturedUser!.IsAdmin);
    }

    [Fact]
    public async Task Handle_FirstUser_ShouldCreateRootFolderWithDeterministicId()
    {
        User? capturedUser = null;
        Folder? capturedFolder = null;

        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .Callback<Folder, CancellationToken>((f, _) => capturedFolder = f)
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedUser);
        Assert.NotNull(capturedFolder);

        var expectedRootFolderId = FolderIdHelper.GenerateRootFolderId(capturedUser!.Id);
        Assert.Equal(expectedRootFolderId, capturedFolder!.Id);
        Assert.Equal(capturedUser.Id, capturedFolder.UserId);
        Assert.Null(capturedFolder.ParentFolderId);
        Assert.Equal(RootFolderKey, capturedFolder.EncryptedKey);
    }

    [Fact]
    public async Task Handle_FirstUser_ShouldCreateRecoveryMethodOfSecretType()
    {
        User? capturedUser = null;
        RecoveryMethod? capturedRecovery = null;

        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Callback<RecoveryMethod, CancellationToken>((rm, _) => capturedRecovery = rm)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedUser);
        Assert.NotNull(capturedRecovery);
        Assert.Equal(capturedUser!.Id, capturedRecovery!.UserId);
        Assert.Equal(RecoveryMethodType.Secret_Recovery, capturedRecovery.RecoveryMethodType);
        Assert.Equal(RecoveryEncryptedMasterKey, capturedRecovery.EncryptedRecoveryData);
        Assert.True(capturedRecovery.IsActive);
    }

    [Fact]
    public async Task Handle_FirstUser_ShouldPersistEncryptedMasterKeyAndSalt()
    {
        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.NotNull(capturedUser);
        Assert.Equal(EncryptedMasterKey, capturedUser!.EncryptedMasterKey);
        Assert.Equal(KeySalt, capturedUser.KeySalt);
        Assert.Equal("opaque-record", capturedUser.RegistrationRecord);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithoutInviteCode_ShouldReturnFailure()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(BuildCommand(inviteCode: null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("An invite code is required to register.", result.Error);
        _userRepositoryMock.Verify(r => r.CreateUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithWhitespaceInviteCode_ShouldReturnFailure()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(BuildCommand(inviteCode: "   "), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("An invite code is required to register.", result.Error);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithUnknownInviteCode_ShouldReturnFailure()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _registerInviteRepositoryMock
            .Setup(r => r.GetByCodeAsync("invalid-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisterInvite?)null);

        var result = await _handler.Handle(BuildCommand(inviteCode: "invalid-code"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid, expired, or already used invite code.", result.Error);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithUsedInviteCode_ShouldReturnFailure()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var usedInvite = new RegisterInvite(
            Guid.NewGuid(), "used-code", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        usedInvite.MarkAsUsed();

        _registerInviteRepositoryMock
            .Setup(r => r.GetByCodeAsync("used-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usedInvite);

        var result = await _handler.Handle(BuildCommand(inviteCode: "used-code"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid, expired, or already used invite code.", result.Error);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithExpiredInviteCode_ShouldReturnFailure()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expiredInvite = new RegisterInvite(
            Guid.NewGuid(), "expired-code", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));

        _registerInviteRepositoryMock
            .Setup(r => r.GetByCodeAsync("expired-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredInvite);

        var result = await _handler.Handle(BuildCommand(inviteCode: "expired-code"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid, expired, or already used invite code.", result.Error);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithValidInvite_ShouldSucceedAndMarkInviteAsUsed()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var validInvite = new RegisterInvite(
            Guid.NewGuid(), "valid-code", DateTime.UtcNow, DateTime.UtcNow.AddHours(24));

        _registerInviteRepositoryMock
            .Setup(r => r.GetByCodeAsync("valid-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validInvite);
        _registerInviteRepositoryMock
            .Setup(r => r.UpdateAsync(validInvite, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(BuildCommand(inviteCode: "valid-code"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(validInvite.IsUsed);
        _registerInviteRepositoryMock.Verify(
            r => r.UpdateAsync(validInvite, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithValidInvite_ShouldNotBeAdmin()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var validInvite = new RegisterInvite(
            Guid.NewGuid(), "valid-code", DateTime.UtcNow, DateTime.UtcNow.AddHours(24));

        _registerInviteRepositoryMock
            .Setup(r => r.GetByCodeAsync("valid-code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(validInvite);
        _registerInviteRepositoryMock
            .Setup(r => r.UpdateAsync(validInvite, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(BuildCommand(inviteCode: "valid-code"), CancellationToken.None);

        Assert.NotNull(capturedUser);
        Assert.False(capturedUser!.IsAdmin);
    }

    [Fact]
    public async Task Handle_NonFirstUserWithValidInvite_ShouldNotPersistWhenInviteFlowShortCircuits()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _registerInviteRepositoryMock
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisterInvite?)null);

        await _handler.Handle(BuildCommand(inviteCode: "missing"), CancellationToken.None);

        _userRepositoryMock.Verify(r => r.CreateUser(It.IsAny<User>()), Times.Never);
        _folderRepositoryMock.Verify(
            r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ShouldReturnFailureAndNotPersist()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User with the same username or email already exists.", result.Error);
        _userRepositoryMock.Verify(r => r.CreateUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNormalizeUsernameAndEmailToLowercase()
    {
        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            BuildCommand(username: "  MiXeDCaSe  ", email: "  MiXeD@Test.COM  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedUser);
        Assert.Equal("mixedcase", capturedUser!.Username);
        Assert.Equal("mixed@test.com", capturedUser.Email);
    }

    [Fact]
    public async Task Handle_WhenEmailIsWhitespace_ShouldPersistNullEmail()
    {
        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(BuildCommand(email: "   "), CancellationToken.None);

        Assert.NotNull(capturedUser);
        Assert.Null(capturedUser!.Email);
    }

    [Fact]
    public async Task Handle_ShouldPersistUserIdentifierUnchanged()
    {
        var identifier = Guid.NewGuid();
        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .Returns(Task.CompletedTask);
        _folderRepositoryMock
            .Setup(r => r.AddFolder(It.IsAny<Folder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Folder f, CancellationToken _) => f);
        _recoveryMethodRepositoryMock
            .Setup(r => r.CreateRecoveryMethod(It.IsAny<RecoveryMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RegisterUserCommand(
            identifier,
            "user",
            "user@test.com",
            "record",
            EncryptedMasterKey,
            KeySalt,
            RecoveryEncryptedMasterKey,
            RootFolderKey,
            null);

        await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(capturedUser);
        Assert.Equal(identifier, capturedUser!.UserIdentifier);
    }

    [Fact]
    public async Task Handle_WhenCreateUserThrows_ShouldReturnFailureWithMessage()
    {
        _userRepositoryMock
            .Setup(r => r.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.UserExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateUser(It.IsAny<User>()))
            .ThrowsAsync(new Exception("DB constraint violation"));

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DB constraint violation", result.Error);
    }

    [Fact]
    public async Task Handle_FirstUser_ShouldNotQueryInviteRepository()
    {
        SetupFirstUserPersist();

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _registerInviteRepositoryMock.Verify(
            r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}