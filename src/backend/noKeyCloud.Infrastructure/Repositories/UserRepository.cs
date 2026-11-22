
using Microsoft.EntityFrameworkCore;
using noKeyCloud.Application.Abstractions.Repositories;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DataContext _context;
    
    public UserRepository(DataContext context)
    {
        _context = context;
    }
    
    public void CreateUser(User user)
    {
        _context.Users.Add(user);
    }

    public async Task<User?> GetUserByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        
        if (normalized.Contains('@'))
        {
            var userByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
            if (userByEmail != null) return userByEmail;
        }
        
        var userByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == normalized, cancellationToken);
        return userByUsername ?? null;
    }
    
    public async Task<User?> GetUserByUserId(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId,
            cancellationToken);
    }
    
    public void RemoveUserByUser(User user)
    {
        _context.Users.Remove(user);
    }
    
    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(cancellationToken);
    }
    
    public async Task<bool> UserExistsAsync(string username, string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return await _context.Users.AnyAsync(u => u.Username == username, cancellationToken);
        }
        return await _context.Users.AnyAsync(u => u.Username == username || u.Email == email, cancellationToken);
    }
    
    public void UpdateUser(User user)
    {
        _context.Users.Update(user);
    }
}