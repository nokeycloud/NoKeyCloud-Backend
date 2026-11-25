using noKeyCloud.Domain.Enums;
using noKeyCloud.Domain.ValueObjects;

namespace noKeyCloud.Domain.Entities;

public class MasterKeyWrap
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public MasterKeyWrapType Type { get; private set; }
    public Guid? WebAuthnCredentialId { get; private set; }
    public byte[] WrappedMasterKey { get; private set; }
    public byte[] Salt { get; private set; }
    public Argon2idParams? Argon2idParams { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public User User { get; private set; } = null!;
    public WebAuthnCredential? WebAuthnCredential { get; private set; }
    
    public MasterKeyWrap(
        Guid id, 
        Guid userId, 
        MasterKeyWrapType type,
        byte[] wrappedMasterKey, 
        byte[] salt,
        Argon2idParams? argon2idParams = null,
        Guid? webAuthnCredentialId = null)
    {
        switch (type)
        {
            case MasterKeyWrapType.OpaquePassword or MasterKeyWrapType.Passphrase when argon2idParams is null:
                throw new ArgumentException($"{type} wraps require Argon2idParams.", nameof(argon2idParams));
            case MasterKeyWrapType.WebAuthn when webAuthnCredentialId is null:
                throw new ArgumentException("WebAuthnPrf wraps require a WebAuthnCredentialId.", nameof(webAuthnCredentialId));
        }

        Id = id;
        UserId = userId;
        Type = type;
        WrappedMasterKey = wrappedMasterKey;
        Salt = salt;
        Argon2idParams = argon2idParams;
        WebAuthnCredentialId = webAuthnCredentialId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private MasterKeyWrap() { }
}