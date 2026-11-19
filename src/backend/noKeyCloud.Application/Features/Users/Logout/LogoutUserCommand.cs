using MediatR;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.Logout;

public record LogoutUserCommand(
    Guid UserId,
    string RefreshToken,
    string? Jti,
    TimeSpan RemainingTtl) : IRequest<Result<LogoutUserResponse>>;