namespace noKeyCloud.Application.Security.Models;

public record OpaqueServerStartLoginResult(
    string ServerLoginState,
    string LoginResponse);