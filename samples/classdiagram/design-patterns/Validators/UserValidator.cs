using DesignPatterns.Domain;
using DesignPatterns.Interfaces;

namespace DesignPatterns.Validators;

/// <summary>
/// User validation rules
/// </summary>
public class UserValidator : IValidator<User>
{
    public bool Validate(User entity)
    {
        return GetValidationErrors(entity).Count() == 0;
    }

    public IEnumerable<string> GetValidationErrors(User entity)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entity.Username))
        {
            errors.Add("Username is required");
        }
        else if (entity.Username.Length < 3)
        {
            errors.Add("Username must be at least 3 characters");
        }

        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            errors.Add("Email is required");
        }
        else if (!entity.Email.Contains('@'))
        {
            errors.Add("Invalid email format");
        }

        if (string.IsNullOrWhiteSpace(entity.FirstName))
        {
            errors.Add("First name is required");
        }

        if (string.IsNullOrWhiteSpace(entity.LastName))
        {
            errors.Add("Last name is required");
        }

        if (string.IsNullOrWhiteSpace(entity.PasswordHash))
        {
            errors.Add("Password is required");
        }

        return errors;
    }
}