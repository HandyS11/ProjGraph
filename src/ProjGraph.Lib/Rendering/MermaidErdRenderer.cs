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

                // Add original type as comment if it differs from sanitized version
                var typeComment = prop.Type != sanitizedType ? $" \"Original: {prop.Type}\"" : "";

                sb.AppendLine($"        {sanitizedType} {prop.Name}{markers}{typeComment}");
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