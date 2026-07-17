using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Globalization;
using System.Text;

namespace ProjGraph.Lib.ClassDiagram.Rendering;

/// <summary>
/// Provides functionality to render a ClassModel as a Mermaid class diagram.
/// </summary>
public sealed class MermaidClassDiagramRenderer : IDiagramRenderer<ClassModel>
{
    /// <inheritdoc />
    public string Format => "mermaid";

    /// <summary>
    /// Renders a ClassModel as a Mermaid class diagram.
    /// </summary>
    /// <param name="model">The ClassModel containing the types, relationships, and an optional title to be rendered.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string representation of the Mermaid class diagram.</returns>
    public string Render(ClassModel model, DiagramOptions? options = null)
    {
        var sb = new StringBuilder();
        var nodeIds = BuildNodeIds(model);

        MermaidFenceHelper.AppendFenceStart(sb, options, model.Title);

        sb.AppendLine("classDiagram");

        foreach (var type in model.Types)
        {
            RenderType(sb, type, nodeIds);
        }

        foreach (var relationship in model.Relationships)
        {
            RenderRelationship(sb, relationship, nodeIds);
        }

        MermaidFenceHelper.AppendFenceEnd(sb, options);

        return sb.ToString();
    }

    /// <summary>
    /// Assigns each type a unique Mermaid node ID. Sanitizing full names is lossy
    /// (<c>Ns.Foo_Bar</c> and <c>Ns.Foo.Bar</c> both map to <c>Ns_Foo_Bar</c>), so distinct
    /// full names that collide after sanitization get a deterministic numeric suffix in
    /// model order. Output only changes when a real collision exists.
    /// </summary>
    /// <param name="model">The model whose types receive IDs.</param>
    /// <returns>A map from type full name to unique Mermaid node ID.</returns>
    private static Dictionary<string, string> BuildNodeIds(ClassModel model)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fullName in model.Types.Select(t => t.FullName))
        {
            if (ids.ContainsKey(fullName))
            {
                continue;
            }

            var baseId = Sanitize(fullName);
            var id = baseId;
            var suffix = 2;
            while (!used.Add(id))
            {
                id = $"{baseId}_{suffix++}";
            }

            ids[fullName] = id;
        }

        return ids;
    }

    /// <summary>
    /// Resolves the Mermaid node ID for a type full name, falling back to plain sanitization
    /// for endpoints that are not part of the model's type list.
    /// </summary>
    /// <param name="ids">The ID map built by <see cref="BuildNodeIds"/>.</param>
    /// <param name="fullName">The type full name to resolve.</param>
    /// <returns>The Mermaid node ID.</returns>
    private static string ResolveId(Dictionary<string, string> ids, string fullName)
    {
        return ids.TryGetValue(fullName, out var id) ? id : Sanitize(fullName);
    }

    /// <summary>
    /// Renders a type definition as a class in a Mermaid class diagram.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> used to construct the Mermaid diagram.</param>
    /// <param name="type">The type definition containing details such as the full name, display name, kind, and members of the type.</param>
    /// <param name="nodeIds">The map from type full name to unique Mermaid node ID.</param>
    private static void RenderType(StringBuilder sb, TypeDefinition type, Dictionary<string, string> nodeIds)
    {
        var sanitizedName = ResolveId(nodeIds, type.FullName);
        var displayName = type.Name;

        // Use generics syntax supported by Mermaid (~T~)
        if (displayName.Contains('<', StringComparison.Ordinal))
        {
            displayName = displayName.Replace('<', '~').Replace('>', '~');
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"    class {sanitizedName} [\"{displayName}\"]");

        // Render stereotypes
        // For interfaces, only render the interface stereotype (they are inherently abstract)
        // Mermaid can only display one stereotype, so prioritize the type kind over abstract
        if (type is { IsAbstract: true, Kind: TypeKind.Class })
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    <<abstract>> {sanitizedName}");
        }

        if (type.Kind != TypeKind.Class)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    <<{type.Kind.ToString().ToLowerInvariant()}>> {sanitizedName}");
        }

        if (type.Members.Count == 0)
        {
            return;
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"    class {sanitizedName} {{");
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
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {visibility}{member.Name}({parameters}) {type}");
        }
        else if (string.IsNullOrEmpty(type))
        {
            // For enum fields, only show the name without type
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {visibility}{member.Name}");
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {visibility}{type} {member.Name}");
        }
    }

    /// <summary>
    /// Renders a relationship between two types in a Mermaid class diagram.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> used to construct the Mermaid diagram.</param>
    /// <param name="relationship">The relationship to be rendered, containing the source, target, and kind of relationship.</param>
    /// <param name="nodeIds">The map from type full name to unique Mermaid node ID.</param>
    private static void RenderRelationship(StringBuilder sb, Relationship relationship, Dictionary<string, string> nodeIds)
    {
        var from = ResolveId(nodeIds, relationship.From);
        var to = ResolveId(nodeIds, relationship.To);
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
            // Association/Dependency: owner on left, referenced type on right.
            // The multiplicity describes the target (owned) end, so in Mermaid it is placed
            // immediately before the target class: {from} {op} "{cardinality}" {to}.
            relationshipStr = $"{from} {op}";

            if (!string.IsNullOrEmpty(relationship.Cardinality))
            {
                relationshipStr += $" \"{relationship.Cardinality}\"";
            }

            relationshipStr += $" {to}";

            // Add label if present
            if (!string.IsNullOrEmpty(relationship.Label))
            {
                relationshipStr += $" : {relationship.Label}";
            }
        }

        sb.Append(CultureInfo.InvariantCulture, $"    {relationshipStr}").AppendLine();
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
    /// <returns>A sanitized string where '.', '`', '&lt;', '&gt;', '[', ']', ',', '+', '?', ':', and ' ' are replaced with '_'.</returns>
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
            .Replace(':', '_')
            .Replace(' ', '_');
    }
}
