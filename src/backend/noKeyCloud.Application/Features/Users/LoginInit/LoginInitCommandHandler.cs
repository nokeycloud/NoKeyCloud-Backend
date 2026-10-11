using MediatR;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.LoginInit;

public class LoginInitCommandHandler(IUserRepository userRepository, IOpaqueServerSetupProvider serverSetupProvider, IOpaqueAuthService opaqueAuthService, IOpaqueSessionStore sessionStore)
    : IRequestHandler<LoginInitCommand, Result<LoginInitResponse>>
{
    public async Task<Result<LoginInitResponse>> Handle(LoginInitCommand request, CancellationToken cancellationToken)
    {
        var identifier = !string.IsNullOrWhiteSpace(request.Username) 
            ? request.Username 
            : request.Email;

        var user = await userRepository.GetUserByUsernameOrEmailAsync(identifier, cancellationToken);
        if (user == null)
        {
            return Result<LoginInitResponse>.Failure("Invalid credentials.");
        }
        
        var serverSetup = await serverSetupProvider.GetServerSetupAsync(cancellationToken);

        try 
        {
            var loginStartResponse = opaqueAuthService.ServerStartLogin(serverSetup, request.StartLoginRequest,
                user.UserIdentifier.ToString(), user.RegistrationRecord);
            
            var sessionId = Guid.NewGuid();
            await sessionStore.SaveSessionAsync(sessionId, user.Id, loginStartResponse.ServerLoginState);

            var response = new LoginInitResponse(
                loginStartResponse.LoginResponse,
                sessionId
            );

            return Result<LoginInitResponse>.Success(response);
        }
        catch (Exception) 
        {
            return Result<LoginInitResponse>.Failure("Invalid client login request");
        }
    }
}