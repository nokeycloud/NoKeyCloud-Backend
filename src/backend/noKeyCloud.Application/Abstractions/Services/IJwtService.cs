namespace noKeyCloud.Application.Abstractions.Services
{
    public interface IJwtService
    {
        Task<string> JwtTokenService(Guid? id, bool isAdmin, Guid sessionId);
    }
}
