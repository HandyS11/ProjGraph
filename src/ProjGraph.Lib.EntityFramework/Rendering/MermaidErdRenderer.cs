using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Text;

namespace ProjGraph.Lib.EntityFramework.Rendering;

/// <summary>
/// Provides functionality to render an Entity-Relationship Diagram (ERD) in Mermaid syntax
/// based on the provided Entity Framework (EF) model.
/// </summary>
public sealed class MermaidErdRenderer : IDiagramRenderer<EfModel>
{
    /// <summary>
    /// Gets or sets a value indicating whether to include the title in the rendered output.
    /// </summary>
    public bool IncludeTitle { get; set; } = true;

    /// <summary>
    /// Renders a Mermaid ERD diagram from the given Entity Framework model.
    /// </summary>
    /// <param name="model">The EF model containing entities and relationships to be rendered.</param>
    /// <returns>
    /// A string representing the ERD in Mermaid syntax, which can be used to visualize the model.
    /// </returns>
    public string Render(EfModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");

        if (IncludeTitle && !string.IsNullOrWhiteSpace(model.ContextName))
        {
            sb.AppendLine("---");
            sb.AppendLine($"title: {model.ContextName}");
            sb.AppendLine("---");
        }

        sb.AppendLine("erDiagram");

        RenderEntities(model, sb);
        RenderRelationships(model, sb);

        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>
    /// Renders all entities with their properties to the StringBuilder.
    /// </summary>
    /// <param name="model">The EF model containing entities to render.</param>
    /// <param name="sb">The StringBuilder to append the rendered output to.</param>
    private static void RenderEntities(EfModel model, StringBuilder sb)
    {
        var sortedEntities = model.Entities.OrderBy(e => e.Name);

        foreach (var entity in sortedEntities)
        {
            sb.AppendLine($"  {entity.Name} {{");

            var orderedProperties = entity.Properties
                .OrderByDescending(p => p.IsPrimaryKey)
                .ThenByDescending(p => p is { IsPrimaryKey: false, IsForeignKey: true })
                .ThenBy(p => p.Name);

            foreach (var propertyLine in orderedProperties.Select(RenderProperty))
            {
                sb.AppendLine($"    {propertyLine}");
            }

            sb.AppendLine("  }");
        }
    }

    /// <summary>
    /// Renders a single property line for an entity.
    /// </summary>
    /// <param name="prop">The property to render.</param>
    /// <returns>A formatted string representing the property in Mermaid syntax.</returns>
    private static string RenderProperty(EfProperty prop)
    {
        var sanitizedType = SanitizeTypeForMermaid(prop.Type);
        if (string.IsNullOrWhiteSpace(sanitizedType))
        {
            sanitizedType = "unknown";
        }

        var markers = BuildKeyMarkers(prop);
        var constraints = BuildConstraintComment(prop);

        return $"{sanitizedType} {prop.Name}{markers}{constraints}";
    }

    /// <summary>
    /// Builds key markers (PK, FK) for a property.
    /// </summary>
    /// <param name="prop">The property to build markers for.</param>
    /// <returns>A formatted string with key markers, or empty string if no markers.</returns>
    private static string BuildKeyMarkers(EfProperty prop)
    {
        var keyMarkers = new List<string>();

        if (prop.IsPrimaryKey)
        {
            keyMarkers.Add("PK");
        }

        if (prop.IsForeignKey)
        {
            keyMarkers.Add("FK");
        }

        return keyMarkers.Count > 0 ? " " + string.Join(",", keyMarkers) : "";
    }

    /// <summary>
    /// Renders all relationships to the StringBuilder.
    /// </summary>
    /// <param name="model">The EF model containing relationships to render.</param>
    /// <param name="sb">The StringBuilder to append the rendered output to.</param>
    private static void RenderRelationships(EfModel model, StringBuilder sb)
    {
        var sortedRelationships = model.Relationships
            .OrderBy(r => r.SourceEntity)
            .ThenBy(r => r.TargetEntity)
            .ThenBy(r => r.Type);

        foreach (var rel in sortedRelationships)
        {
            var relSyntax = GetRelationshipSyntax(rel);
            var sourceEntity = rel.SourceEntity.Trim();
            var targetEntity = rel.TargetEntity.Trim();

            sb.AppendLine($"  {sourceEntity} {relSyntax} {targetEntity} : \"\"");
        }
    }

    /// <summary>
    /// Gets the Mermaid syntax for a relationship based on its type.
    /// </summary>
    /// <param name="rel">The relationship to get syntax for.</param>
    /// <returns>The Mermaid relationship syntax string.</returns>
    private static string GetRelationshipSyntax(EfRelationship rel)
    {
        return rel.Type switch
        {
            EfRelationshipType.OneToOne => rel.IsRequired ? "||--||" : "|o--||",
            EfRelationshipType.OneToMany => rel.IsRequired ? "||--o{" : "|o--o{",
            EfRelationshipType.ManyToMany => "}|--|{",
            _ => "--"
        };
    }

    /// <summary>
    /// Builds a constraint comment for a given property in an entity model.
    /// The comment includes information about the property's original type (if different from the sanitized type),
    /// as well as any constraints such as required, maximum length, precision, scale, or default value.
    /// </summary>
    /// <param name="prop">The property for which the constraint comment is being built.</param>
    /// <returns>
    /// A formatted string containing the constraint comment. If no constraints are present, an empty string is returned.
    /// </returns>
    private static string BuildConstraintComment(EfProperty prop)
    {
        var commentParts = new List<string>();

        // Add constraints
        var constraints = new List<string>();

        if (prop is { IsPrimaryKey: false } &&
            (prop.IsExplicitlyRequired || prop is { IsRequired: true, IsValueType: false }))
        {
            constraints.Add("required");
        }

        if (prop.MaxLength.HasValue)
        {
            constraints.Add($"max:{prop.MaxLength}");
        }

        if (prop.Precision.HasValue)
        {
            constraints.Add(prop.Scale.HasValue
                ? $"precision({prop.Precision},{prop.Scale})"
                : $"precision:{prop.Precision}");
        }

        if (!string.IsNullOrEmpty(prop.DefaultValue))
        {
            constraints.Add($"default:{prop.DefaultValue}");
        }

        if (constraints.Count > 0)
        {
            commentParts.Add(string.Join(", ", constraints));
        }

        // Return formatted comment if there's anything to show
        if (commentParts.Count > 0)
        {
            return $" \"{string.Join(" | ", commentParts)}\"";
        }

        return "";
    }

    /// <summary>
    /// Sanitizes a given type name for use in Mermaid diagrams by removing or replacing 
    /// characters that are not supported in Mermaid syntax.
    /// </summary>
    /// <param name="type">The original type name to be sanitized.</param>
    /// <returns>A sanitized string where unsupported characters are removed or replaced.</returns>
    private static string SanitizeTypeForMermaid(string type)
    {
        return type
            .Replace("?", "") // Remove nullable markers
            .Replace("<", "~") // Replace generic brackets
            .Replace(">", "~")
            .Replace("[", "") // Remove array brackets
            .Replace("]", "");
    }
}