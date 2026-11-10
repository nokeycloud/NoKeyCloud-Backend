namespace noKeyCloud.Application.Security.Models;

public sealed record RefreshTokenInfo(Guid UserId, Guid SessionId);