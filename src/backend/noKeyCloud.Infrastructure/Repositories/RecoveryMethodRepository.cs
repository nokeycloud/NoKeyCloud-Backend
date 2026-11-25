using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Repositories;

public class RecoveryMethodRepository : IRecoveryMethodRepository
{
    private readonly DataContext _context;

    public RecoveryMethodRepository(DataContext context)
    {
        _context = context;
    }
    
    public void Add(RecoveryMethod recoveryMethod)
    {
        _context.RecoveryMethods.Add(recoveryMethod);
    }
    
}