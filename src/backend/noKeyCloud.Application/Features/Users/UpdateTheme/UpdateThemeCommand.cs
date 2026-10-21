using MediatR;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.UpdateTheme;

public record UpdateThemeCommand(Guid UserId, string Theme) : IRequest<Result>;