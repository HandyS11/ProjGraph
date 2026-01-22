using DesignPatterns.Domain;
using DesignPatterns.Enums;

namespace DesignPatterns.Repositories;

/// <summary>
/// User-specific repository with custom queries
/// </summary>
public class UserRepository : Repository<User>
{
    public async Task<User?> GetByEmailAsync(string email)
    {
        var users = await FindAsync(u => u.Email == email);
        return users.FirstOrDefault();
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var users = await FindAsync(u => u.Username == username);
        return users.FirstOrDefault();
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        return await FindAsync(u => u.IsActive && !u.IsDeleted);
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
    {
        return await FindAsync(u => u.Role == role);
    }
}