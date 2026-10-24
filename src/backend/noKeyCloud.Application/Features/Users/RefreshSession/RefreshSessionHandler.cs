using MediatR;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.RefreshSession;

public class RefreshSessionHandler(IJwtService jwtService, IRefreshTokenProvider refreshTokenProvider, IUserRepository userRepository, IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<RefreshSessionCommand, Result<RefreshSessionResult>>
{
    private readonly JwtOptions _options = jwtOptions.Value;
    public async Task<Result<RefreshSessionResult>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = await refreshTokenProvider.GetUserIdByRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (userId == null)
        {
            return Result<RefreshSessionResult>.Failure("Invalid or expired refresh token.");
        }

        var storedToken = await refreshTokenProvider.GetRefreshTokenAsync(userId.Value, cancellationToken);

        if (string.IsNullOrEmpty(storedToken) || storedToken != request.RefreshToken)
        {
            return Result<RefreshSessionResult>.Failure("Invalid or expired refresh token.");
        }

        var newRefreshToken = refreshTokenProvider.GenerateRefreshToken();

        if (string.IsNullOrEmpty(newRefreshToken))
        {
            return Result<RefreshSessionResult>.Failure("Failed to generate a new refresh token.");
        }
        await refreshTokenProvider.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);

        await refreshTokenProvider.StoreRefreshTokenAsync(userId.Value, newRefreshToken, TimeSpan.FromHours(_options.RefreshTokenLifetimeHours), cancellationToken);

        var user = await userRepository.GetUserByUserId(userId.Value, cancellationToken);
        var newJwt = await jwtService.JwtTokenService(userId.Value, user!.IsAdmin);

        if (string.IsNullOrEmpty(newJwt))
        {
            return Result<RefreshSessionResult>.Failure("Failed to generate a new JWT token.");
        }

        var responsePayload = new RefreshSessionResponse(DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes));

        var applicationResult = new RefreshSessionResult(responsePayload, newJwt, newRefreshToken);

        return Result<RefreshSessionResult>.Success(applicationResult);
    }
}