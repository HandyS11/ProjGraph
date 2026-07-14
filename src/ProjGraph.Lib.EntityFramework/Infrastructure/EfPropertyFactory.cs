using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Factory for <see cref="EfProperty"/> instances: creates modified copies (init-only setters make
/// in-place mutation impossible) and gets-or-creates named properties on an entity with type inference.
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

    /// <summary>
    /// Gets an existing property or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="entity">The entity containing the property.</param>
    /// <param name="propName">The property name.</param>
    /// <param name="type">The property type.</param>
    /// <returns>The EfProperty object.</returns>
    public static EfProperty GetOrCreateProperty(EfEntity entity, string propName, string type)
    {
        var property = entity.Properties.FirstOrDefault(p => p.Name == propName);
        if (property is null)
        {
            var detectedType = type;
            if (string.IsNullOrEmpty(detectedType))
            {
                detectedType =
                    propName.EndsWith(EfAnalysisConstants.Suffixes.IdSuffix, StringComparison.OrdinalIgnoreCase)
                        ? EfAnalysisConstants.DataTypes.Guid
                        : EfAnalysisConstants.DataTypes.StringTypeName;
            }

            property = new EfProperty
            {
                Name = propName,
                Type = detectedType,
                IsValueType = IsValueTypeString(detectedType)
            };
            entity.Properties.Add(property);
        }
        else if (!string.IsNullOrEmpty(type))
        {
            var updated = CopyWith(property, new EfPropertyOverrides
            {
                Type = type,
                IsValueType = IsValueTypeString(type)
            });
            var index = entity.Properties.IndexOf(property);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }

            property = updated;
        }

        return property;
    }

    /// <summary>
    /// Determines whether a type name represents a value type.
    /// </summary>
    /// <param name="type">The type name to check.</param>
    public static bool IsValueTypeString(string type)
    {
        var typeName = type.TrimEnd('?');
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            typeName = typeName[(typeName.LastIndexOf('.') + 1)..];
        }

        return EfAnalysisConstants.DataTypes.ValueTypes.Contains(typeName);
    }
}
