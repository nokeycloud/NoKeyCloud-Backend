using MediatR;
using noKeyCloud.Contracts.Authenticate;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.RegisterInit;

public record RegisterResponseUserCommand(
    string UserIdentifier,
    string RegistrationRequest) : IRequest<Result<RegisterResponseUserResponse>>;