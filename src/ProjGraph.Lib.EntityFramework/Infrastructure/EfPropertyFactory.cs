using ProjGraph.Core.Models;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Factory for creating modified copies of <see cref="EfProperty"/> instances.
/// Since <see cref="EfProperty"/> uses init-only setters, this helper creates new instances
/// with specific values overridden while preserving all other property values.
/// </summary>
internal static class EfPropertyFactory
{
    /// <summary>
    /// Creates a copy of the given <see cref="EfProperty"/> with the specified values overridden.
    /// Any property left as <c>null</c> in <paramref name="overrides"/> will retain the value from the source property.
    /// </summary>
    /// <param name="source">The source property to copy values from.</param>
    /// <param name="overrides">The set of property overrides to apply.</param>
    public static EfProperty CopyWith(EfProperty source, EfPropertyOverrides overrides)
    {
        return new EfProperty
        {
            Name = source.Name,
            Type = overrides.Type ?? source.Type,
            IsPrimaryKey = overrides.IsPrimaryKey ?? source.IsPrimaryKey,
            IsForeignKey = overrides.IsForeignKey ?? source.IsForeignKey,
            IsRequired = overrides.IsRequired ?? source.IsRequired,
            IsValueType = overrides.IsValueType ?? source.IsValueType,
            IsExplicitlyRequired = overrides.IsExplicitlyRequired ?? source.IsExplicitlyRequired,
            MaxLength = overrides.MaxLength ?? source.MaxLength,
            Precision = overrides.Precision ?? source.Precision,
            Scale = overrides.Scale ?? source.Scale,
            DefaultValue = overrides.DefaultValue ?? source.DefaultValue
        };
    }
}
