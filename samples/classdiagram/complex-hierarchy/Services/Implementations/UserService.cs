using ComplexHierarchy.Domain.Models;
using ComplexHierarchy.Repositories.Interfaces;
using ComplexHierarchy.Services.Base;

namespace ComplexHierarchy.Services.Implementations;

public class UserService(IUserRepository userRepository) : ServiceBase<User>(userRepository)
{
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await userRepository.GetByEmailAsync(email);
    }
}