using MediatR;
using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Application.Features.Users.LoginVerify;

public record LoginVerifyCommand(
    string SessionId,
    string FinishLoginRequest) : IRequest<Result<LoginVerifyResult>>;