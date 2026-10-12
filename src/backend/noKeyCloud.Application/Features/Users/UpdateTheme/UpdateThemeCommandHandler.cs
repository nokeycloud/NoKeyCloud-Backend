using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.UpdateTheme;

public class UpdateThemeCommandHandler(IUserRepository userRepository) : IRequestHandler<UpdateThemeCommand, Result>
{
    public async Task<Result> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetUserByUserId(request.UserId, cancellationToken);
        if (user == null) return Result.Failure("User not found.");

        user.UpdateTheme(request.Theme);
        await userRepository.UpdateUserAsync(user, cancellationToken);
        
        return Result.Success();
    }
}