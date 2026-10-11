namespace noKeyCloud.Contracts.Authenticate;

public record RegisterResponseUserRequest(
    string UserIdentifier,
    string RegistrationRequest);