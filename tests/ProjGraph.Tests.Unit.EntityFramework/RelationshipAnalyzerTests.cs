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

    /// <summary>
    /// Live-found bug: EF Core owned types (OwnsOne/OwnsMany) can legally declare navigation
    /// properties on their CLR class — nothing stops an owned class from referencing back to a root
    /// entity. AnalyzeRelationships walked every entity in the dictionary, including owned ones, as a
    /// potential relationship SOURCE with no <see cref="EfEntity.IsOwned"/> filter, so that navigation
    /// fabricated a real <see cref="EfRelationship"/> naming the owned entity's bare, non-unique
    /// <see cref="EfEntity.Name"/> — even though no EfRelationship is ever supposed to exist for an
    /// owned type (the renderer derives the identifying-relationship line from the owned entities it
    /// draws as boxes instead). Reproduces the shape a reviewer found live by adding
    /// <c>Ticket ParentTicket</c> to the owned <c>SeatLocation</c> fixture class.
    /// </summary>
    [Fact]
    public void AnalyzeRelationships_OwnedEntityClrTypeHasNavigationToRoot_CreatesNoRelationshipForIt()
    {
        const string source = """
            public class Flight
            {
                public int Id { get; set; }
            }
            public class Cabin
            {
                public string Layout { get; set; } = "";
                public Flight ParentFlight { get; set; } = null!;
            }
            """;
        var compilation = RoslynTestHelper.CreateCompilation(source);

        var flightSymbol = RoslynTestHelper.GetTypeSymbol(compilation, "Flight")!;
        var flight = EntityAnalyzer.AnalyzeEntity(flightSymbol);

        var cabinSymbol = RoslynTestHelper.GetTypeSymbol(compilation, "Cabin")!;
        var cabin = new EfEntity
        {
            Name = "Cabin",
            Key = "Flight.Cabin",
            IsOwned = true,
            OwnerEntity = "Flight",
            NavigationName = "Cabin"
        };
        foreach (var property in EntityAnalyzer.AnalyzeEntity(cabinSymbol).Properties)
        {
            cabin.Properties.Add(property);
        }

        // Entities dictionary keyed exactly as production code keys it: root entities by Name, owned
        // entities by EffectiveKey ("{Owner}.{Nav}") — see FluentOwnedTypeWalker.Capture.
        var entities = new Dictionary<string, EfEntity>
        {
            ["Flight"] = flight,
            ["Flight.Cabin"] = cabin
        };
        var model = new EfModel();
        model.Entities.Add(flight);
        model.Entities.Add(cabin);

        RelationshipAnalyzer.AnalyzeRelationships(model, entities, compilation);

        model.Relationships.Should().BeEmpty(
            "no EfRelationship is ever created for an owned type, even when its CLR class has a real, " +
            "legal-in-EF-Core navigation property back to a root entity");
    }
}
