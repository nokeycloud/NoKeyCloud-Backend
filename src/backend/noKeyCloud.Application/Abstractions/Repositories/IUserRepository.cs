using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Application.Abstractions.Repositories;

public interface IUserRepository
{
    void CreateUser(User user);
    Task<User?> GetUserByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken);
    Task<User?> GetUserByUserId(Guid userId, CancellationToken cancellationToken);
    void RemoveUserByUser(User user);
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(string username, string? email, CancellationToken cancellationToken);
    void UpdateUser(User user);
}