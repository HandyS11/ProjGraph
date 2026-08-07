namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Represents a set of optional overrides for creating a modified copy of an <see cref="ProjGraph.Core.Models.EfProperty"/>.
/// Any property left as <c>null</c> will retain the value from the source property.
/// </summary>
internal sealed record EfPropertyOverrides
{
    /// <summary>Override for the Type property.</summary>
    public string? Type { get; init; }

    /// <summary>Override for the IsPrimaryKey property.</summary>
    public bool? IsPrimaryKey { get; init; }

    /// <summary>Override for the IsForeignKey property.</summary>
    public bool? IsForeignKey { get; init; }

    /// <summary>Override for the IsRequired property.</summary>
    public bool? IsRequired { get; init; }

    /// <summary>Override for the IsValueType property.</summary>
    public bool? IsValueType { get; init; }

    /// <summary>Override for the IsExplicitlyRequired property.</summary>
    public bool? IsExplicitlyRequired { get; init; }

    /// <summary>Override for the IsTypeInferred property.</summary>
    public bool? IsTypeInferred { get; init; }

    /// <summary>Override for the MaxLength property.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Override for the Precision property.</summary>
    public int? Precision { get; init; }

    /// <summary>Override for the Scale property.</summary>
    public int? Scale { get; init; }

    /// <summary>Override for the DefaultValue property.</summary>
    public string? DefaultValue { get; init; }
}
