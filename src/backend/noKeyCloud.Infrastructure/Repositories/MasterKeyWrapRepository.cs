using Microsoft.EntityFrameworkCore;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Domain.Entities;
using noKeyCloud.Domain.Enums;

namespace noKeyCloud.Infrastructure.Repositories;

public class MasterKeyWrapRepository(DataContext context) : IMasterKeyWrapRepository
{
    public void Add(MasterKeyWrap masterKeyWrap)
    {
        context.MasterKeyWraps.Add(masterKeyWrap);
    }

    public void Delete(MasterKeyWrap masterKeyWrap)
    {
        context.MasterKeyWraps.Remove(masterKeyWrap);
    }

    public async Task<MasterKeyWrap?> GetByUserIdAndTypeAsync(Guid userId, MasterKeyWrapType masterKeyWrapType, CancellationToken cancellationToken = default)
    {
        return await context.MasterKeyWraps.FirstOrDefaultAsync(w => w.UserId == userId && w.Type == masterKeyWrapType, cancellationToken);
    }

    public void Update(MasterKeyWrap masterKeyWrap)
    {
        context.MasterKeyWraps.Update(masterKeyWrap);
    }
}