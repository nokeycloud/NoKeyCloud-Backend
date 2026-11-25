namespace noKeyCloud.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public string KeyPrefix { get; private set; }
    public string KeyHash { get; private set; }
    public string[] Scopes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public User User { get; private set; } = null!;

    public bool IsActive => RevokedAt is null && (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);

    public ApiKey(Guid id, Guid userId, string name, string keyPrefix, string keyHash, string[] scopes, DateTime? expiresAt)
    {
        Id = id;
        UserId = userId;
        Name = name;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        Scopes = scopes;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkUsed() => LastUsedAt = DateTime.UtcNow;
    public void Revoke() => RevokedAt = DateTime.UtcNow;

    private ApiKey() { }
}