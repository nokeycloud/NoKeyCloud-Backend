using MediatR;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;
using noKeyCloud.Domain.Enums;

namespace noKeyCloud.Application.Features.Users.LoginVerify;

    public class LoginVerifyCommandHandler(
        IAuthService authService,
        IFolderRepository folderRepository,
        IUserRepository userRepository,
        IMasterKeyWrapRepository masterKeyWrapRepository,
        IOptions<JwtOptions> jwtOptions)
        : IRequestHandler<LoginVerifyCommand, Result<LoginVerifyResult>>
    {
        private readonly JwtOptions _options = jwtOptions.Value;

    public async Task<Result<LoginVerifyResult>> Handle(LoginVerifyCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionIdGuid))
            return Result<LoginVerifyResult>.Failure("Invalid session ID format.");

        var session = await authService.OpaqueSessions.GetServerLoginStateAsync(sessionIdGuid);

        if (session == null) return Result<LoginVerifyResult>.Failure("Session not found.");

        try
        {
            var sessionKey = authService.Opaque.ServerFinishLogin(session, request.FinishLoginRequest);
            
            var nullableUserId = await authService.OpaqueSessions.GetUserIdAsync(sessionIdGuid);
            if (nullableUserId == null)
            {
                return Result<LoginVerifyResult>.Failure("Could not retrieve user associated with the session.");
            }

            var userId = nullableUserId.Value;
            var user = await userRepository.GetUserByUserId(userId, cancellationToken);
            var authSessionId = Guid.NewGuid();
            var token = await authService.Jwt.JwtTokenService(userId, user!.IsAdmin, authSessionId);

            var refreshToken = authService.RefreshTokens.GenerateRefreshToken();
            await authService.RefreshTokens.StoreRefreshTokenAsync(userId, authSessionId, refreshToken, TimeSpan.FromHours(_options.RefreshTokenLifetimeHours), cancellationToken);
            var rootFolder = await folderRepository.GetUserHomeFolder(userId, cancellationToken);

            var deleteSession = await authService.OpaqueSessions.DeleteSessionAsync(sessionIdGuid);

            if (!deleteSession) return Result<LoginVerifyResult>.Failure("Could not remove session");
            
            var masterKey = await masterKeyWrapRepository.GetByUserIdAndTypeAsync(userId, MasterKeyWrapType.OpaquePassword, cancellationToken);
            if (masterKey is null)
            {
                return Result<LoginVerifyResult>.Failure("Master key not found.");
            }
            
            var responsePayload = new LoginVerifyResponse(
                masterKey.WrappedMasterKey,
                masterKey.Salt);
            
            var handlerResult = new LoginVerifyResult(
                responsePayload, 
                token, 
                refreshToken
            );

            return Result<LoginVerifyResult>.Success(handlerResult);
        }
        catch (Exception)
        {
            await authService.OpaqueSessions.DeleteSessionAsync(sessionIdGuid);
            return Result<LoginVerifyResult>.Failure("Login verification failed");
        }
    }
}