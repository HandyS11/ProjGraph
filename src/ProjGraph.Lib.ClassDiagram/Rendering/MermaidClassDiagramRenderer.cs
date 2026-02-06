using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Text;

namespace ProjGraph.Lib.ClassDiagram.Rendering;

/// <summary>
/// Provides functionality to render a ClassModel as a Mermaid class diagram.
/// </summary>
public sealed class MermaidClassDiagramRenderer : IDiagramRenderer<ClassModel>
{
    /// <summary>
    /// Renders a ClassModel as a Mermaid class diagram.
    /// </summary>
    /// <param name="model">The ClassModel containing the types, relationships, and an optional title to be rendered.</param>
    /// <returns>A string representation of the Mermaid class diagram.</returns>
    public string Render(ClassModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");

        if (!string.IsNullOrEmpty(model.Title))
        {
            sb.AppendLine("---");
            sb.AppendLine($"title: {model.Title}");
            sb.AppendLine("---");
        }

        sb.AppendLine("classDiagram");

        foreach (var type in model.Types)
        {
            RenderType(sb, type);
        }

        foreach (var relationship in model.Relationships)
        {
            RenderRelationship(sb, relationship);
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>
    /// Renders a type definition as a class in a Mermaid class diagram.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> used to construct the Mermaid diagram.</param>
    /// <param name="type">The type definition containing details such as the full name, display name, kind, and members of the type.</param>
    private static void RenderType(StringBuilder sb, TypeDefinition type)
    {
        var sanitizedName = Sanitize(type.FullName);
        var displayName = type.Name;

        // Use generics syntax supported by Mermaid (~T~)
        if (displayName.Contains('<'))
        {
            displayName = displayName.Replace('<', '~').Replace('>', '~');
        }

        sb.AppendLine($"    class {sanitizedName} [\"{displayName}\"]");

        // Render stereotypes
        // For interfaces, only render the interface stereotype (they are inherently abstract)
        // Mermaid can only display one stereotype, so prioritize the type kind over abstract
        if (type is { IsAbstract: true, Kind: TypeKind.Class })
        {
            sb.AppendLine($"    <<abstract>> {sanitizedName}");
        }

        if (type.Kind != TypeKind.Class)
        {
            sb.AppendLine($"    <<{type.Kind.ToString().ToLower()}>> {sanitizedName}");
        }

        if (type.Members.Count <= 0)
        {
            return;
        }

        sb.AppendLine($"    class {sanitizedName} {{");
        foreach (var member in type.Members)
        {
            RenderMember(sb, member);
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Renders a member of a class in a Mermaid class diagram.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> used to construct the Mermaid diagram.</param>
    /// <param name="member">The member definition containing details such as name, type, visibility, and kind.</param>
    private static void RenderMember(StringBuilder sb, MemberDefinition member)
    {
        var visibility = GetVisibilityChar(member.Visibility);
        var type = member.Type.Replace('<', '~').Replace('>', '~');

        if (member.Kind == MemberKind.Method)
        {
            var parameters = member.Parameters != null
                ? string.Join(", ",
                    member.Parameters.Select(p => $"{p.Type.Replace('<', '~').Replace('>', '~')} {p.Name}"))
                : "";
            sb.AppendLine($"        {visibility}{member.Name}({parameters}) {type}");
        }
        else if (string.IsNullOrEmpty(type))
        {
            // For enum fields, only show the name without type
            sb.AppendLine($"        {visibility}{member.Name}");
        }
        else
        {
            sb.AppendLine($"        {visibility}{type} {member.Name}");
        }
    }

    /// <summary>
    /// Renders a relationship between two types in a Mermaid class diagram.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> used to construct the Mermaid diagram.</param>
    /// <param name="relationship">The relationship to be rendered, containing the source, target, and kind of relationship.</param>
    private static void RenderRelationship(StringBuilder sb, Relationship relationship)
    {
        var from = Sanitize(relationship.From);
        var to = Sanitize(relationship.To);
        var op = relationship.Kind switch
        {
            RelationshipKind.Inheritance => "<|--",
            RelationshipKind.Realization => "<|..",
            RelationshipKind.Association => "-->",
            RelationshipKind.Dependency => "..>",
            _ => "-->"
        };

        // Build the relationship string
        // For Inheritance and Realization: To <|-- From (base <|-- derived)
        // For Association and Dependency: From --> To (owner --> owned)
        string relationshipStr;

        if (relationship.Kind is RelationshipKind.Inheritance or RelationshipKind.Realization)
        {
            // Inheritance/Realization: base class on left, derived class on right
            relationshipStr = $"{to} {op} {from}";
        }
        else
        {
            // Association/Dependency: owner on left, referenced type on right
            relationshipStr = $"{from}";

            // Add cardinality on target (To) side if present
            if (!string.IsNullOrEmpty(relationship.Cardinality))
            {
                relationshipStr += $" \"{relationship.Cardinality}\"";
            }

            relationshipStr += $" {op} {to}";

            // Add label if present
            if (!string.IsNullOrEmpty(relationship.Label))
            {
                relationshipStr += $" : {relationship.Label}";
            }
        }

        sb.AppendLine($"    {relationshipStr}");
    }

    /// <summary>
    /// Gets the visibility character corresponding to the specified visibility level.
    /// </summary>
    /// <param name="visibility">The visibility level of a member (e.g., Public, Protected, Internal, Private).</param>
    /// <returns>
    /// A character representing the visibility:
    /// '+' for public, '#' for protected, '~' for internal, '-' for private.
    /// Defaults to '+' for unknown visibility levels.
    /// </returns>
    private static char GetVisibilityChar(Visibility visibility)
    {
        return visibility switch
        {
            Visibility.Public => '+',
            Visibility.Protected => '#',
            Visibility.Internal => '~',
            Visibility.Private => '-',
            _ => '+'
        };
    }

    /// <summary>
    /// Sanitizes a given string by replacing certain characters with underscores.
    /// </summary>
    /// <param name="name">The input string to be sanitized.</param>
    /// <returns>A sanitized string where '.', '`', '&lt;', '&gt;', '[', ']', ',', '+', '?', and ' ' are replaced with '_'.</returns>
    private static string Sanitize(string name)
    {
        return name
            .Replace('.', '_')
            .Replace('`', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('[', '_')
            .Replace(']', '_')
            .Replace(',', '_')
            .Replace('+', '_')
            .Replace('?', '_')
            .Replace(' ', '_');
    }
}






