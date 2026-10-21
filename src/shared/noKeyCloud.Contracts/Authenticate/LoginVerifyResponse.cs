namespace noKeyCloud.Contracts.Authenticate;

public record LoginVerifyResponse (
    byte[] EncryptedMasterKey,
    byte[] KeySalt);
