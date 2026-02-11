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
    /// Any parameter left as <c>null</c> will retain the value from the source property.
    /// </summary>
    /// <param name="source">The source property to copy values from.</param>
    /// <param name="type">Override for the Type property.</param>
    /// <param name="isPrimaryKey">Override for the IsPrimaryKey property.</param>
    /// <param name="isForeignKey">Override for the IsForeignKey property.</param>
    /// <param name="isRequired">Override for the IsRequired property.</param>
    /// <param name="isValueType">Override for the IsValueType property.</param>
    /// <param name="isExplicitlyRequired">Override for the IsExplicitlyRequired property.</param>
    /// <param name="maxLength">Override for the MaxLength property.</param>
    /// <param name="precision">Override for the Precision property.</param>
    /// <param name="scale">Override for the Scale property.</param>
    /// <param name="defaultValue">Override for the DefaultValue property.</param>
    public static EfProperty CopyWith(
        EfProperty source,
        string? type = null,
        bool? isPrimaryKey = null,
        bool? isForeignKey = null,
        bool? isRequired = null,
        bool? isValueType = null,
        bool? isExplicitlyRequired = null,
        int? maxLength = null,
        int? precision = null,
        int? scale = null,
        string? defaultValue = null)
    {
        return new EfProperty
        {
            Name = source.Name,
            Type = type ?? source.Type,
            IsPrimaryKey = isPrimaryKey ?? source.IsPrimaryKey,
            IsForeignKey = isForeignKey ?? source.IsForeignKey,
            IsRequired = isRequired ?? source.IsRequired,
            IsValueType = isValueType ?? source.IsValueType,
            IsExplicitlyRequired = isExplicitlyRequired ?? source.IsExplicitlyRequired,
            MaxLength = maxLength ?? source.MaxLength,
            Precision = precision ?? source.Precision,
            Scale = scale ?? source.Scale,
            DefaultValue = defaultValue ?? source.DefaultValue
        };
    }
}
