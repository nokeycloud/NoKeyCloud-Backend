namespace noKeyCloud.Domain.Entities;

public class OpaqueCredential
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string RegistrationRecord { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; private set; }
    public User User { get; private set; } = null!;

    public OpaqueCredential(Guid id, Guid userId, string registrationRecord)
    {
        Id = id;
        UserId = userId;
        RegistrationRecord = registrationRecord;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
    
    public void UpdateRegistrationRecord(string newRegistrationRecord)
    {
        RegistrationRecord = newRegistrationRecord;
        UpdatedAt = DateTime.UtcNow;
    }
    
    private OpaqueCredential() {}
}