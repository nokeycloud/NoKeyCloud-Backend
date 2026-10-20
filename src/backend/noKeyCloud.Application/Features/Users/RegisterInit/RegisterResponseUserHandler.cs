using MediatR;
using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.RegisterInit;

public class RegisterResponseUserHandler(IOpaqueAuthService opaqueAuthService, IOpaqueServerSetupProvider serverSetupProvider) : IRequestHandler<RegisterResponseUserCommand, Result<RegisterResponseUserResponse>>
{
    public async Task<Result<RegisterResponseUserResponse>> Handle(RegisterResponseUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserIdentifier) ||
            string.IsNullOrWhiteSpace(request.RegistrationRequest))
        {
            return Result<RegisterResponseUserResponse>.Failure(
                "User identifier and registration request are required.");
        }
        
        var serverSetup = await serverSetupProvider.GetServerSetupAsync(cancellationToken);
        
        var registrationResponse = opaqueAuthService.ServerRegisterResponse(serverSetup, request.UserIdentifier, request.RegistrationRequest);
        
        return Result<RegisterResponseUserResponse>.Success(
            new RegisterResponseUserResponse(registrationResponse));
        
    }
}