namespace noKeyCloud.Domain.Entities;

public class WebAuthnCredential
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public byte[] CredentialId { get; private set; }
    public byte[] PublicKey { get; private set; }
    public uint SignCount { get; private set; }
    public Guid AaGuid { get; private set; }
    public string[] Transports { get; private set; }
    public bool IsBackupEligible { get; private set; }
    public bool IsBackedUp { get; private set; }
    public bool PrfSupported { get; private set; }
    public byte[]? PrfSalt { get; private set; }
    public string DisplayName { get; private set; }
    public int CoseAlgorithm { get; private set; }
    public bool IsPostQuantum => CoseAlgorithm is -48 or -49 or -50;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    public User User { get; private set; } = null!;

    public WebAuthnCredential(
        Guid id, Guid userId, byte[] credentialId, byte[] publicKey, uint signCount,
        Guid aaGuid, string[] transports, bool isBackupEligible, bool isBackedUp,
        bool prfSupported, byte[]? prfSalt, string displayName)
    {
        Id = id;
        UserId = userId;
        CredentialId = credentialId;
        PublicKey = publicKey;
        SignCount = signCount;
        AaGuid = aaGuid;
        Transports = transports;
        IsBackupEligible = isBackupEligible;
        IsBackedUp = isBackedUp;
        PrfSupported = prfSupported;
        PrfSalt = prfSalt;
        DisplayName = displayName;
        CreatedAt = DateTime.UtcNow;
    }

    public void RecordUsage(uint newSignCount, bool isBackedUp)
    {
        SignCount = newSignCount;
        IsBackedUp = isBackedUp;
        LastUsedAt = DateTime.UtcNow;
    }

    private WebAuthnCredential() { }
}