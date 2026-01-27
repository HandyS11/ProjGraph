using ComplexHierarchy.Domain.Enums;

namespace ComplexHierarchy.Domain.Models;

public class User : Person
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public AccessLevel AccessLevel { get; set; }
}