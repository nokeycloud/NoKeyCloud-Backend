namespace noKeyCloud.Contracts.Authenticate;

public record LoginVerifyRequest
(
    string SessionId,
    string FinishLoginRequest
);