using DesignPatterns.Base;
using DesignPatterns.Enums;

namespace DesignPatterns.Domain;

/// <summary>
/// User entity with roles and orders
/// </summary>
public class User : AuditableEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<Order> Orders { get; set; } = new();
    public ShoppingCart? ShoppingCart { get; set; }
}
