namespace noKeyCloud.Domain.Entities;

public class ExternalLogin
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; }
    public string ProviderSubject { get; private set; }
    public DateTime LinkedAt { get; private set; }

    public User User { get; private set; } = null!;

    public ExternalLogin(Guid id, Guid userId, string provider, string providerSubject)
    {
        Id = id;
        UserId = userId;
        Provider = provider;
        ProviderSubject = providerSubject;
        LinkedAt = DateTime.UtcNow;
    }

    private ExternalLogin() { }
}