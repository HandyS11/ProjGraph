namespace DesignPatterns.Base;

/// <summary>
/// Base entity class with primary key
/// </summary>
public abstract class Entity : IEntity
{
    public int Id { get; set; }
}