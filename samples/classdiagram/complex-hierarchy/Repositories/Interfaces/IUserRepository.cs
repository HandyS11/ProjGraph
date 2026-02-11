using ComplexHierarchy.Domain.Models;

namespace ComplexHierarchy.Repositories.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}
