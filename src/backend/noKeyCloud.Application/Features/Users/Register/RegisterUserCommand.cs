using MediatR;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.Register;

public record RegisterUserCommand(
    Guid UserIdentifier,
    string Username,
    string Email,
    string RegistrationRecord,
    byte[] EncryptedMasterKey,
    byte[] KeySalt,
    byte[] RecoveryEncryptedMasterKey,
    byte[] RootFolderKey,
    string? RegisterInviteCode) : IRequest<Result>;