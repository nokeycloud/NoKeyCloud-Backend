
using noKeyCloud.Contracts.Common;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task CreateUser(User user);
    Task<User?> GetUserByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken);
    Task<User?> GetUserByUserId(Guid userId, CancellationToken cancellationToken);
    Task<Result> RemoveUserByUser(User user, CancellationToken cancellationToken);
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(string username, string? email, CancellationToken cancellationToken);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
}