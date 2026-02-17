using DesignPatterns.Interfaces;

namespace DesignPatterns.Validators;

/// <summary>
/// Composite pattern: Aggregates multiple IValidator implementations
/// </summary>
public class CompositeValidator<T>(IEnumerable<IValidator<T>> validators) : IValidator<T>
{
    private readonly IEnumerable<IValidator<T>> _validators = validators;

    public bool Validate(T entity) => _validators.All(v => v.Validate(entity));

    public IEnumerable<string> GetValidationErrors(T entity) =>
        _validators.SelectMany(v => v.GetValidationErrors(entity));
}
