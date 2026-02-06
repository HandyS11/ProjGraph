using ProjGraph.Core.Models;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Constants;

namespace ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Extensions;

/// <summary>
/// Provides extension methods for <see cref="EfRelationship"/>.
/// </summary>
public static class RelationshipExtensions
{
    /// <summary>
    /// Generates a unique key for an entity relationship based on its type and the involved entities.
    /// </summary>
    /// <param name="relationship">The <see cref="EfRelationship"/> representing the relationship to generate the key for.</param>
    /// <returns>
    /// A string representing the unique key for the relationship. For symmetric relationships (One-to-One or Many-to-Many),
    /// the entity names are sorted alphabetically to avoid duplicates. For One-to-Many relationships, the direction is preserved.
    /// </returns>
    public static string GenerateKey(this EfRelationship relationship)
    {
        // For symmetric relationships (1:1, M:M), sort entity names to avoid duplicates
        if (relationship.Type is EfRelationshipType.OneToOne or EfRelationshipType.ManyToMany)
        {
            var entitiesSorted = new[] { relationship.SourceEntity, relationship.TargetEntity }
                .OrderBy(e => e)
                .ToArray();
            return
                $"{entitiesSorted[0]}{EfAnalysisConstants.RelationshipKeys.Delimiter}{entitiesSorted[1]}{EfAnalysisConstants.RelationshipKeys.Delimiter}{relationship.Type}";
        }

        // For OneToMany, direction matters
        return
            $"{relationship.SourceEntity}{EfAnalysisConstants.RelationshipKeys.Delimiter}{relationship.TargetEntity}{EfAnalysisConstants.RelationshipKeys.Delimiter}{relationship.Type}";
    }
}

