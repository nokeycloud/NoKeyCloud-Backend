namespace noKeyCloud.Contracts.Authenticate;

public record LoginInitResponse(
    string LoginResponse,
    Guid SessionId
    );