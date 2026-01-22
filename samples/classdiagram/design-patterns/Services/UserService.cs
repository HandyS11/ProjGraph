using DesignPatterns.Domain;
using DesignPatterns.Interfaces;

namespace DesignPatterns.Services;

/// <summary>
/// User service with business logic
/// </summary>
public class UserService
{
    private readonly IRepository<User> _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IValidator<User> _validator;

    public UserService(
        IRepository<User> userRepository,
        INotificationService notificationService,
        IValidator<User> validator)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _validator = validator;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        if (!_validator.Validate(user))
        {
            throw new InvalidOperationException("User validation failed");
        }

        var createdUser = await _userRepository.AddAsync(user);
        await _notificationService.SendEmailAsync(
            user.Email,
            "Welcome",
            $"Welcome {user.FirstName}!"
        );
        return createdUser;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _userRepository.GetByIdAsync(id);
    }

    public async Task UpdateUserAsync(User user)
    {
        if (!_validator.Validate(user))
        {
            throw new InvalidOperationException("User validation failed");
        }

        await _userRepository.UpdateAsync(user);
    }

    public async Task DeleteUserAsync(int id)
    {
        await _userRepository.DeleteAsync(id);
    }
}