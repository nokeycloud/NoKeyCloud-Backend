using Microsoft.EntityFrameworkCore;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Repositories;

public class RegisterInviteRepository(DataContext context) : IRegisterInviteRepository
{
    public void Add(RegisterInvite invite)
    {
        context.RegisterInvites.Add(invite);
    }

    public async Task<RegisterInvite?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await context.RegisterInvites
            .SingleOrDefaultAsync(i => i.Code == code, cancellationToken);
    }

    public void Update(RegisterInvite invite)
    {
        context.RegisterInvites.Update(invite);
    }
    
    public async Task<IEnumerable<RegisterInvite>> GetActiveInvitesAsync(CancellationToken cancellationToken = default)
    {
        return await context.RegisterInvites
            .Where(i => !i.IsUsed && (i.ExpiresAt == null || i.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<RegisterInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.RegisterInvites.FindAsync(new object[] { id }, cancellationToken);
    }

    public void Delete(RegisterInvite invite)
    {
        context.RegisterInvites.Remove(invite);
    }
}