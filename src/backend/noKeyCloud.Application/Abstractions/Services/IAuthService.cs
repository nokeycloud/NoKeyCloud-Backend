namespace noKeyCloud.Application.Abstractions.Services;

public interface IAuthService
{
    IJwtProvider Jwt { get; }
    IRefreshTokenProvider RefreshTokens { get; }
    IOpaqueAuthService Opaque { get; }
    IOpaqueSessionStore OpaqueSessions { get; }
}