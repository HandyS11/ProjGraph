namespace DesignPatterns.Base;

/// <summary>
/// Base entity with audit fields for tracking changes
/// </summary>
public abstract class AuditableEntity : Entity
{
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
}
