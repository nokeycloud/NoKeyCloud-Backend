using noKeyCloud.Application.Security.Models;

namespace noKeyCloud.Application.Abstractions.Services;

public interface IOpaqueAuthService
{
    string ServerRegisterResponse(string serverSetup,
        string userIdentifier,
        string registrationRequest);
    
    OpaqueServerStartLoginResult ServerStartLogin(
        string serverSetup,
        string startClientLoginResult,
        string userIdentifier,
        string registrationRecord);

    string ServerFinishLogin(
        string startServerLoginResult,
        string finishClientLoginResult);
}