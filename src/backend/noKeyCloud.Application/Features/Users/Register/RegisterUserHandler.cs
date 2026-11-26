using System.Text;
using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Domain.Entities;
using noKeyCloud.Application.Features.Folders;
using noKeyCloud.Domain.Enums;
using noKeyCloud.Domain.ValueObjects;

namespace noKeyCloud.Application.Features.Users.Register;

public class RegisterUserHandler(
    IUserRepository userRepository, 
    IFolderRepository folderRepository, 
    IRegisterInviteRepository registerInviteRepository, 
    IRecoveryMethodRepository recoveryMethodRepository, 
    IOpaqueCredentialRepository opaqueCredentialRepository, 
    IMasterKeyWrapRepository masterKeyWrapRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, Result>
{
    public async Task<Result> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var isFirstUser = !await userRepository.HasAnyUsersAsync(cancellationToken);
        RegisterInvite? validInvite = null;
        
        
        if (!isFirstUser)
        {
            if (string.IsNullOrWhiteSpace(request.RegisterInviteCode))
                return Result.Failure("An invite code is required to register.");
            
            validInvite = await registerInviteRepository.GetByCodeAsync(request.RegisterInviteCode, cancellationToken);
            
            if (validInvite == null || !validInvite.IsValid())
                return Result.Failure("Invalid, expired, or already used invite code.");
        }
        
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        
        var userExists = await userRepository.UserExistsAsync(normalizedUsername, normalizedEmail, cancellationToken);
        if (userExists)
        {
            return Result.Failure("User with the same username or email already exists.");
        }
        
        try
        {
            var userId = Guid.NewGuid();
            
            var user = new User(
                userId,
                request.UserIdentifier,
                normalizedEmail, 
                normalizedUsername, 
                isAdmin: isFirstUser);

            var temporaryNameBytes = Array.Empty<byte>();
            
            var now = DateTime.UtcNow;
            
            var rootFolderId = FolderIdHelper.GenerateRootFolderId(user.Id);

            var rootFolder = new Folder(
              id: rootFolderId,
              encryptedName: temporaryNameBytes,
              encryptedKey: request.RootFolderKey,
              createdAt: now,
              updatedAt: now,
              parentFolderId: null,
              userId: user.Id
            );
            
            var recoveryMethod = new RecoveryMethod(
                Guid.NewGuid(),
                RecoveryMethodType.Secret_Recovery,
                request.RecoveryEncryptedMasterKey,
                userId);

            var registrationRecord = new OpaqueCredential(
                Guid.NewGuid(),
                userId,
                request.RegistrationRecord);

            var masterKeyWrap = new MasterKeyWrap(
                Guid.NewGuid(),
                userId,
                MasterKeyWrapType.OpaquePassword,
                request.EncryptedMasterKey,
                request.KeySalt,
                Argon2idParams.Default);
                

            userRepository.CreateUser(user);
            folderRepository.Add(rootFolder);
            recoveryMethodRepository.Add(recoveryMethod);
            opaqueCredentialRepository.Add(registrationRecord);
            masterKeyWrapRepository.Add(masterKeyWrap);

            if (validInvite != null)
            {
                validInvite.MarkAsUsed();
                registerInviteRepository.Update(validInvite);
            }
            
            await unitOfWork.SaveChangesAsync(cancellationToken);

        }
        catch (Exception e)
        {
            return Result.Failure(e.Message);
        }

        return Result.Success();
    }
}