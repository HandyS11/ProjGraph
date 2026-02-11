using ComplexHierarchy.Domain.Base;

namespace ComplexHierarchy.Domain.Models;

public abstract class Person : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public Address? PrimaryAddress { get; set; }
}
