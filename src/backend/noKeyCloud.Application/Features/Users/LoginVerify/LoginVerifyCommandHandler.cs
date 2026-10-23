using MediatR;
using Microsoft.Extensions.Options;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Configuration;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.LoginVerify;

    public class LoginVerifyCommandHandler(
        IJwtService jwtService,
        IOpaqueSessionStore sessionStore,
        IRefreshTokenProvider refreshTokenProvider,
        IFolderRepository folderRepository,
        IUserRepository userRepository,
        IOpaqueAuthService opaqueAuthService,
        IOptions<JwtOptions> jwtOptions)
        : IRequestHandler<LoginVerifyCommand, Result<LoginVerifyResult>>
    {
        private readonly JwtOptions _options = jwtOptions.Value;

    public async Task<Result<LoginVerifyResult>> Handle(LoginVerifyCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionIdGuid))
            return Result<LoginVerifyResult>.Failure("Invalid session ID format.");

        var session = await sessionStore.GetServerLoginStateAsync(sessionIdGuid);

        if (session == null) return Result<LoginVerifyResult>.Failure("Session not found.");

        try
        {
            var sessionKey = opaqueAuthService.ServerFinishLogin(session, request.FinishLoginRequest);
            
            var nullableUserId = await sessionStore.GetUserIdAsync(sessionIdGuid);
            if (nullableUserId == null)
            {
                return Result<LoginVerifyResult>.Failure("Could not retrieve user associated with the session.");
            }

            var userId = nullableUserId.Value;
            var user = await userRepository.GetUserByUserId(userId, cancellationToken);
            var token = await jwtService.JwtTokenService(userId, user!.IsAdmin);

            var refreshToken = refreshTokenProvider.GenerateRefreshToken();
            await refreshTokenProvider.StoreRefreshTokenAsync(userId, refreshToken, TimeSpan.FromHours(_options.RefreshTokenLifetimeHours), cancellationToken);
            var rootFolder = await folderRepository.GetUserHomeFolder(userId, cancellationToken);

            var deleteSession = await sessionStore.DeleteSessionAsync(sessionIdGuid);

            if (!deleteSession) return Result<LoginVerifyResult>.Failure("Could not remove session");
            
            var responsePayload = new LoginVerifyResponse(
                user.EncryptedMasterKey,
                user.KeySalt);
            
            var handlerResult = new LoginVerifyResult(
                responsePayload, 
                token, 
                refreshToken
            );

            return Result<LoginVerifyResult>.Success(handlerResult);
        }
        catch (Exception)
        {
            await sessionStore.DeleteSessionAsync(sessionIdGuid);
            return Result<LoginVerifyResult>.Failure("Login verification failed");
        }
    }
}