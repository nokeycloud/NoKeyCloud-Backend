using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.RemoveUser;

public class RemoveUserCommandHandler(IUserRepository userRepository, IAuthService authService, IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveUserCommand, Result<RemoveUserResponse>>
{
    public async Task<Result<RemoveUserResponse>> Handle(RemoveUserCommand request, CancellationToken cancellationToken)
    {
        var userId = request.Id;
        var user = await userRepository.GetUserByUserId(userId, cancellationToken);
        
        if (user == null) return Result<RemoveUserResponse>.Failure("User not found");

        try
        {
            userRepository.RemoveUserByUser(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            return Result<RemoveUserResponse>.Failure(e.Message);
        }

        await authService.RefreshTokens.RevokeAllSessionsAsync(userId, cancellationToken);
        
        var response = new RemoveUserResponse("User successfully removed");

        return Result<RemoveUserResponse>.Success(response);
    }
}