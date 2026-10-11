using noKeyCloud.Application.Abstractions.Services;
using noKeyCloud.Application.Security.Models;
using OPAQUE.Net;

namespace noKeyCloud.Infrastructure.Security.Opaque;

public class OpaqueAuthService() : IOpaqueAuthService
{
    public string ServerRegisterResponse(string serverSetup, string userIdentifier,
        string registrationRequest)
    {
        var opaqueServer = new OpaqueServer();
        if (!opaqueServer.CreateRegistrationResponse(
                serverSetup,
                userIdentifier,
                registrationRequest,
                out var registrationResponse))
        {
            throw new InvalidOperationException("Failed to create OPAQUE server registration response");
        }
        return registrationResponse!;
    }

    public OpaqueServerStartLoginResult ServerStartLogin(string serverSetup,
        string startClientLoginResult,
        string userIdentifier, string registrationRecord)
    {
        var opaqueServer = new OpaqueServer();
        if (!opaqueServer.StartLogin(
                serverSetup, 
                startClientLoginResult, 
                userIdentifier, 
                registrationRecord, 
                out var result,
                out var exception))
        {
            throw new InvalidOperationException("Failed to start OPAQUE server login.", exception);
        }

        return new OpaqueServerStartLoginResult(
            result!.ServerLoginState,
            result.LoginResponse);
    }

    public string ServerFinishLogin(string startServerLoginResult,
        string finishClientLoginResult)
    {
        var opaqueServer = new OpaqueServer();
        if (!opaqueServer.FinishLogin(
                startServerLoginResult, 
                finishClientLoginResult,
                out var sessionKey))
        {
            throw new InvalidOperationException("Failed server finish login step.");
        }
        return sessionKey!;
    }
}