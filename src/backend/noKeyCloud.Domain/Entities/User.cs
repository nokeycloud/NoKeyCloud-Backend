

using noKeyCloud.Contracts.Common;

namespace noKeyCloud.Domain.Entities;

public class User
{
    //
    
    public Guid Id { get; private set; }
    
    public Guid UserIdentifier { get; private set; }
    
    public string? Email { get; private set; }
    
    public string Username { get; private set; }
    
    public bool IsActive { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    
    public DateTime UpdatedAt { get; private set; }
    
    public string RegistrationRecord { get; private set; }
    
    public byte[] EncryptedMasterKey { get; private set; }
    
    public byte[] KeySalt { get; private set; }
    
    public bool IsAdmin { get; private set; }
    
    //Navigation
    
    private readonly List<UserKey> _userKeys = new();
    public IReadOnlyCollection<UserKey> UserKeys => _userKeys.AsReadOnly();
    
    private readonly List<Folder> _folders = new();
    public IReadOnlyCollection<Folder> Folders => _folders.AsReadOnly();
    
    private readonly List<File> _files = new();
    public IReadOnlyCollection<File> Files => _files.AsReadOnly();
    
    private readonly List<PublicLink> _publicLinks = new();
    public IReadOnlyCollection<PublicLink> PublicLinks => _publicLinks.AsReadOnly();
    
    private readonly List<UserShare> _userShares = new();
    public IReadOnlyCollection<UserShare> UserShares => _userShares.AsReadOnly();
    
    private readonly List<RecoveryMethod> _recoveryMethods = new();
    public IReadOnlyCollection<RecoveryMethod> RecoveryMethods => _recoveryMethods.AsReadOnly();
    
    private readonly List<Session> _sessions = new();
    public IReadOnlyCollection<Session> Sessions => _sessions.AsReadOnly();
    
    private readonly List<SecurityEvent> _securityEvents = new();
    public IReadOnlyCollection<SecurityEvent> SecurityEvents => _securityEvents.AsReadOnly();
    
    //Validation

    public User(Guid id, Guid userIdentifier,string? email, string username, string registrationRecord, byte[] encryptedMasterKey, byte[] keySalt, bool isAdmin = false)
    {
        Id = id;
        UserIdentifier = userIdentifier;
        Email = email;
        Username = username;
        IsActive = true;
        RegistrationRecord = registrationRecord;
        EncryptedMasterKey = encryptedMasterKey;
        KeySalt = keySalt;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        IsAdmin = isAdmin;
    }
    
    //EF
    private User()
    {
    }

}