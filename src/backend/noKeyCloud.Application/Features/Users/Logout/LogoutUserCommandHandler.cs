using MediatR;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.Logout;

public class LogoutUserCommandHandler(IRefreshTokenProvider refreshTokenProvider, IJwtRevocationService jwtRevocationService)
    : IRequestHandler<LogoutUserCommand, Result<LogoutUserResponse>>
{
    public async Task<Result<LogoutUserResponse>> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    {
        var tokenInfo = await refreshTokenProvider.FindByTokenAsync(request.RefreshToken, cancellationToken);

        if (tokenInfo == null || tokenInfo.UserId != request.UserId)
        {
            return Result<LogoutUserResponse>.Failure("Token.NotFound: Token is invalid or already removed.");
        }

        await refreshTokenProvider.RevokeSessionAsync(tokenInfo.UserId, tokenInfo.SessionId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Jti) && request.RemainingTtl > TimeSpan.Zero)
        {
            await jwtRevocationService.RevokeAsync(request.Jti, request.RemainingTtl, cancellationToken);
        }
        
        return Result<LogoutUserResponse>.Success(new LogoutUserResponse("User logged out successfully."));
    }
}