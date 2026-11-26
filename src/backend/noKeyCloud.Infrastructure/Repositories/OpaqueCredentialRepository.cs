using Microsoft.EntityFrameworkCore;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Repositories;

public class OpaqueCredentialRepository(DataContext context) : IOpaqueCredentialRepository
{
    public void Add(OpaqueCredential credential)
    {
        context.OpaqueCredentials.Add(credential);
    }

    public void Delete(OpaqueCredential credential)
    {
        context.OpaqueCredentials.Remove(credential);
    }

    public async Task<OpaqueCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.OpaqueCredentials.SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public void Update(OpaqueCredential credential)
    {
        context.OpaqueCredentials.Update(credential);
    }
}