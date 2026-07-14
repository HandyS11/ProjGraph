using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="RelationshipAnalyzer"/>: many-to-many join synthesis and the
/// direct-relationship cleanup that runs afterwards.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class RelationshipAnalyzerTests
{
    private static (Dictionary<string, EfEntity> Entities, EfModel Model, Compilation Compilation) Build(
        string source,
        params string[] entityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();
        foreach (var name in entityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol(compilation, name)!;
            var entity = EntityAnalyzer.AnalyzeEntity(symbol);
            entities[name] = entity;
            model.Entities.Add(entity);
        }

        return (entities, model, compilation);
    }

    [Fact]
    public void AnalyzeRelationships_ManyToManyPlusOneToManyBetweenSamePair_KeepsTheOneToMany()
    {
        // User and Group have a skip-navigation many-to-many, plus a separate Group.Owner -> User
        // reference (a one-to-many). Converting the M2M into a join table must not also drop the
        // co-existing one-to-many between the same pair.
        const string source = """
            using System.Collections.Generic;
            public class User
            {
                public int Id { get; set; }
                public List<Group> Groups { get; set; } = [];
                public List<Group> OwnedGroups { get; set; } = [];
            }
            public class Group
            {
                public int Id { get; set; }
                public List<User> Members { get; set; } = [];
                public int OwnerId { get; set; }
                public User Owner { get; set; } = null!;
            }
            """;
        var (entities, model, compilation) = Build(source, "User", "Group");

        RelationshipAnalyzer.AnalyzeRelationships(model, entities, compilation);

        // The M2M is replaced by a join table, so both User and Group point at it.
        model.Entities.Should().Contain(e => e.IsJoinEntity);

        // The legitimate one-to-many directly between User and Group must survive the cleanup.
        var pair = new[] { "User", "Group" };
        model.Relationships.Should().Contain(r =>
            r.Type == EfRelationshipType.OneToMany
            && pair.Contains(r.SourceEntity)
            && pair.Contains(r.TargetEntity));
    }
}
