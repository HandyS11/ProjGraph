namespace DesignPatterns.Interfaces;

/// <summary>
/// Generic validator interface
/// </summary>
/// <typeparam name="T">Type to validate</typeparam>
public interface IValidator<T>
{
    bool Validate(T entity);
    IEnumerable<string> GetValidationErrors(T entity);
}