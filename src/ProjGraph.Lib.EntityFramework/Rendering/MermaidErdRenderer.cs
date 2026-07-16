using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using System.Globalization;
using System.Text;

namespace ProjGraph.Lib.EntityFramework.Rendering;

/// <summary>
/// Provides functionality to render an Entity-Relationship Diagram (ERD) in Mermaid syntax
/// based on the provided Entity Framework (EF) model.
/// </summary>
public sealed class MermaidErdRenderer : IDiagramRenderer<EfModel>
{
    /// <inheritdoc />
    public string Format => "mermaid";

    /// <summary>
    /// Renders a Mermaid ERD diagram from the given Entity Framework model.
    /// </summary>
    /// <param name="model">The EF model containing entities and relationships to be rendered.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>
    /// A string representing the ERD in Mermaid syntax, which can be used to visualize the model.
    /// </returns>
    public string Render(EfModel model, DiagramOptions? options = null)
    {
        var sb = new StringBuilder();

        MermaidFenceHelper.AppendFenceStart(sb, options, model.ContextName);

        sb.AppendLine("erDiagram");

        RenderEntities(model, sb, options);
        RenderRelationships(model, sb, options);

        MermaidFenceHelper.AppendFenceEnd(sb, options);

        return sb.ToString();
    }

    /// <summary>
    /// Renders all entities with their properties to the StringBuilder.
    /// </summary>
    /// <param name="model">The EF model containing entities to render.</param>
    /// <param name="sb">The StringBuilder to append the rendered output to.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    private static void RenderEntities(EfModel model, StringBuilder sb, DiagramOptions? options)
    {
        var rendered = model.Entities
            .Where(e => !IsInlined(e, model, options))
            .OrderBy(e => e.Name);

        foreach (var entity in rendered)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {SanitizeEntityName(DisplayName(entity, model, options))} {{");

            var orderedProperties = EffectiveProperties(entity, model, options)
                .OrderByDescending(p => p.IsPrimaryKey)
                .ThenByDescending(p => p is { IsPrimaryKey: false, IsForeignKey: true })
                .ThenBy(p => p.Name);

            foreach (var propertyLine in orderedProperties.Select(RenderProperty))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {propertyLine}");
            }

            sb.AppendLine("  }");
        }
    }

    /// <summary>
    /// Returns the table an entity effectively maps to: its explicit table name, or its entity name
    /// when unmapped (EF's default).
    /// </summary>
    /// <param name="entity">The entity.</param>
    private static string EffectiveTable(EfEntity entity)
        => string.IsNullOrEmpty(entity.TableName) ? entity.Name : entity.TableName;

    /// <summary>
    /// Determines whether an owned entity's columns are folded into its owner rather than drawn as their
    /// own box: true when it shares the owner's table (EF table-splitting). Always false in Classic mode.
    /// </summary>
    /// <param name="entity">The candidate entity.</param>
    /// <param name="model">The model, used to resolve the owner.</param>
    /// <param name="options">The render options carrying the owned mode.</param>
    /// <param name="visited">
    /// The set of entity keys already visited in this recursion, used to guard against ownership cycles.
    /// Forwarded to <see cref="HasEffectiveProperties"/> so a cycle is detected regardless of whether it is
    /// reached directly or through this hop; callers outside the mutual recursion should omit it.
    /// </param>
    private static bool IsInlined(EfEntity entity, EfModel model, DiagramOptions? options, HashSet<string>? visited = null)
    {
        if (!entity.IsOwned || (options?.ErdOwnedMode ?? ErdOwnedMode.MirrorEf) == ErdOwnedMode.Classic)
        {
            return false;
        }

        // An owned collection is never inlined: folding a to-many into flat scalar columns on the owner
        // is meaningless. EF never maps one to the owner's table, so this enforces the invariant.
        if (entity.IsCollection)
        {
            return false;
        }

        // An owned type that resolves to zero effective columns (its own properties, and — recursively —
        // any inlined child's) must still surface as its own box rather than vanish: the spec's error-
        // handling contract promises "an empty owned box rather than dropped data" for an owned type whose
        // CLR type could not be resolved. Inlining it here would fold zero columns onto the owner and
        // leave no trace it was ever configured — silent data loss, not a degraded-but-visible result.
        if (!HasEffectiveProperties(entity, model, options, visited))
        {
            return false;
        }

        var owner = model.Entities.FirstOrDefault(e => e.EffectiveKey == entity.OwnerEntity);
        return owner is not null && EffectiveTable(owner) == EffectiveTable(entity);
    }

    /// <summary>
    /// Determines whether an owned entity would contribute at least one rendered column: one of its own
    /// properties, or — recursively — one contributed by a child owned entity that would itself be inlined
    /// into it. Mirrors <see cref="EffectiveProperties"/>'s recursion, including its cycle guard: the same
    /// <paramref name="visited"/> set is threaded through the <see cref="IsInlined"/> hop rather than let
    /// each call start a fresh set, since <see cref="IsInlined"/> itself calls back into this method. Does
    /// not materialize the full property sequence, since <see cref="IsInlined"/> only needs to know whether
    /// it is empty.
    /// </summary>
    /// <param name="entity">The candidate entity.</param>
    /// <param name="model">The model, used to resolve owned children.</param>
    /// <param name="options">The render options carrying the owned mode.</param>
    /// <param name="visited">The set of entity keys already visited in this recursion, used to guard against ownership cycles.</param>
    private static bool HasEffectiveProperties(
        EfEntity entity, EfModel model, DiagramOptions? options, HashSet<string>? visited = null)
    {
        if (entity.Properties.Count > 0)
        {
            return true;
        }

        visited ??= [];
        if (!visited.Add(entity.EffectiveKey))
        {
            return false;
        }

        return model.Entities
            .Where(e => e.OwnerEntity == entity.EffectiveKey && IsInlined(e, model, options, visited))
            .Any(child => HasEffectiveProperties(child, model, options, visited));
    }

    /// <summary>
    /// Returns the properties rendered for an entity: its own, plus the prefixed columns of every owned
    /// entity inlined into it (recursively, so nested ownership compounds prefixes as EF does).
    /// </summary>
    /// <param name="entity">The entity being rendered.</param>
    /// <param name="model">The model.</param>
    /// <param name="options">The render options.</param>
    /// <param name="visited">The set of entity keys already visited in this recursion, used to guard against ownership cycles.</param>
    private static IEnumerable<EfProperty> EffectiveProperties(
        EfEntity entity, EfModel model, DiagramOptions? options, HashSet<string>? visited = null)
    {
        foreach (var property in entity.Properties)
        {
            yield return property;
        }

        // Nothing in the model type prevents a self-owning or mutually-owning entity, and unbounded
        // recursion would raise StackOverflowException — uncatchable, killing the CLI/MCP process.
        visited ??= [];
        if (!visited.Add(entity.EffectiveKey))
        {
            yield break;
        }

        var inlinedChildren = model.Entities
            .Where(e => e.OwnerEntity == entity.EffectiveKey && IsInlined(e, model, options));

        foreach (var child in inlinedChildren)
        {
            foreach (var property in EffectiveProperties(child, model, options, visited))
            {
                // EF names table-split owned columns Nav_Property; nested ownership compounds the prefix.
                yield return EfPropertyFactory.Rename(property, $"{child.NavigationName}_{property.Name}");
            }
        }
    }

    /// <summary>
    /// Returns the label for an entity box: its simple name, qualified to its flattened key
    /// (<c>Owner_Nav</c>, or <c>Owner_Nav1_Nav2</c> for nested ownership) when another rendered entity
    /// shares that name (two owners may own the same CLR type, which EF treats as distinct entity types).
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="model">The model, used to detect name collisions.</param>
    /// <param name="options">The render options, used to tell which entities are actually drawn.</param>
    private static string DisplayName(EfEntity entity, EfModel model, DiagramOptions? options)
    {
        if (!entity.IsOwned)
        {
            return entity.Name;
        }

        // Only entities actually drawn can collide on the diagram; an inlined owned entity is never
        // drawn, so it must not force a qualified label onto the only box of that name.
        var collides = model.Entities.Count(e => e.Name == entity.Name && !IsInlined(e, model, options)) > 1;

        // Qualify by the entity's own key ({Owner}.{Nav}, dots flattened), which is unique by
        // construction. Qualifying by the OWNER's CLR name is not enough: for nested owned types whose
        // immediate owners share a CLR name (Invoice.ShipTo and Invoice.BillTo both "Address", each
        // owning a Geo), it yields the same "Address_Geo" identifier for two different boxes, which
        // Mermaid silently merges into one.
        return collides ? entity.EffectiveKey.Replace('.', '_') : entity.Name;
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
    /// <param name="options">The options for rendering the diagram.</param>
    private static void RenderRelationships(EfModel model, StringBuilder sb, DiagramOptions? options)
    {
        var sortedRelationships = model.Relationships
            .OrderBy(r => r.SourceEntity)
            .ThenBy(r => r.TargetEntity)
            .ThenBy(r => r.Type);

        foreach (var rel in sortedRelationships)
        {
            var relSyntax = GetRelationshipSyntax(rel);
            var sourceEntity = SanitizeEntityName(rel.SourceEntity.Trim());
            var targetEntity = SanitizeEntityName(rel.TargetEntity.Trim());

            sb.AppendLine(CultureInfo.InvariantCulture, $"  {sourceEntity} {relSyntax} {targetEntity} : \"\"");
        }

        // Owned relationships are derived, never stored: an inlined owned type must have no line, and
        // deriving here keeps that decision in the same place as the inlining decision.
        var ownedBoxes = model.Entities
            .Where(e => e.IsOwned && !IsInlined(e, model, options))
            .OrderBy(e => e.OwnerEntity)
            .ThenBy(e => e.Name);

        foreach (var owned in ownedBoxes)
        {
            var owner = model.Entities.FirstOrDefault(e => e.EffectiveKey == owned.OwnerEntity);
            if (owner is null)
            {
                continue;
            }

            var syntax = owned.IsCollection ? "||--o{" : "||--||";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {SanitizeEntityName(DisplayName(owner, model, options))} {syntax} " +
                $"{SanitizeEntityName(DisplayName(owned, model, options))} : \"{owned.NavigationName}\"");
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
    /// Sanitizes an entity name for use as a Mermaid ER entity identifier. Names that are not bare
    /// identifiers (e.g. generic types such as <c>IdentityUserRole&lt;string&gt;</c>) are wrapped in
    /// double quotes so Mermaid accepts them; simple identifiers are returned unchanged.
    /// </summary>
    /// <param name="name">The entity name to sanitize.</param>
    /// <returns>The entity name, quoted if it contains characters outside <c>[A-Za-z0-9_]</c>.</returns>
    private static string SanitizeEntityName(string name)
    {
        var isBareIdentifier = name.Length > 0 &&
                               name.All(c => char.IsLetterOrDigit(c) || c == '_');

        return isBareIdentifier ? name : $"\"{name}\"";
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
            .Replace("?", "", StringComparison.Ordinal) // Remove nullable markers
            .Replace("<", "~", StringComparison.Ordinal) // Replace generic brackets
            .Replace(">", "~", StringComparison.Ordinal)
            .Replace("[", "", StringComparison.Ordinal) // Remove array brackets
            .Replace("]", "", StringComparison.Ordinal);
    }
}
