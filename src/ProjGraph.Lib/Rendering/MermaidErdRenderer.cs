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
                var type = SanitizeTypeForMermaid(prop.Type);
                var pk = prop.IsPrimaryKey ? " PK" : "";
                var fk = prop.IsForeignKey ? " FK" : "";
                sb.AppendLine($"        {type} {prop.Name}{pk}{fk}");
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

            var label = string.IsNullOrEmpty(rel.Label) ? "" : $" : \"{rel.Label}\"";
            sb.AppendLine($"    {rel.SourceEntity} {relSyntax} {rel.TargetEntity}{label}");
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