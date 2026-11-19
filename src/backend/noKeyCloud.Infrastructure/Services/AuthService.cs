using noKeyCloud.Application.Abstractions.Services;

namespace noKeyCloud.Infrastructure.Services;

public class AuthService(IJwtProvider jwtProvider,
    IRefreshTokenProvider refreshTokenProvider,
    IOpaqueAuthService opaqueAuthService,
    IOpaqueSessionStore opaqueSessionStore) : IAuthService
{
    public IJwtProvider Jwt => jwtProvider;
    public IRefreshTokenProvider RefreshTokens => refreshTokenProvider;
    public IOpaqueAuthService Opaque => opaqueAuthService;
    public IOpaqueSessionStore OpaqueSessions => opaqueSessionStore;
}