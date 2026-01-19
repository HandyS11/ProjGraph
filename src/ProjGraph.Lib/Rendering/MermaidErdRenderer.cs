using ProjGraph.Core.Models;
using System.Text;

namespace ProjGraph.Lib.Rendering;

public static class MermaidErdRenderer
{
    public static string Render(EfModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("erDiagram");

        foreach (var entity in model.Entities)
        {
            sb.AppendLine($"    {entity.Name} {{");
            foreach (var prop in entity.Properties)
            {
                var sanitizedType = SanitizeTypeForMermaid(prop.Type);

                // Build key markers (comma-separated for Mermaid syntax)
                var keyMarkers = new List<string>();
                if (prop.IsPrimaryKey)
                {
                    keyMarkers.Add("PK");
                }

                if (prop.IsForeignKey)
                {
                    keyMarkers.Add("FK");
                }

                var markers = keyMarkers.Count > 0 ? " " + string.Join(",", keyMarkers) : "";

                // Build constraint comment
                var constraints = BuildConstraintComment(prop);

                sb.AppendLine($"        {sanitizedType} {prop.Name}{markers}{constraints}");
            }

            sb.AppendLine("    }");
        }

        foreach (var rel in model.Relationships)
        {
            var relSyntax = rel.Type switch
            {
                EfRelationshipType.OneToOne => "||--||",
                EfRelationshipType.OneToMany => rel.IsRequired ? "||--o{" : "|o--o{",
                EfRelationshipType.ManyToMany => "}|--|{",
                _ => "--"
            };

            // Always include label for consistent Mermaid syntax
            var sourceEntity = rel.SourceEntity.Trim();
            var targetEntity = rel.TargetEntity.Trim();
            var label = string.IsNullOrWhiteSpace(rel.Label) ? "" : rel.Label;

            sb.AppendLine($"    {sourceEntity} {relSyntax} {targetEntity} : \"{label}\"");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string BuildConstraintComment(EfProperty prop)
    {
        var commentParts = new List<string>();

        // Add original type if different from sanitized
        var sanitizedType = SanitizeTypeForMermaid(prop.Type);
        if (prop.Type != sanitizedType)
        {
            commentParts.Add(prop.Type);
        }

        // Add constraints
        var constraints = new List<string>();

        if (prop is { IsRequired: true, IsPrimaryKey: false })
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