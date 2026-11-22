using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IRecoveryMethodRepository
{
    void Add(RecoveryMethod recoveryMethod);
}