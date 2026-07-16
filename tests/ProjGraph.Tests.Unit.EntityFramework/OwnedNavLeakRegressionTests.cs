using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// End-to-end regression test for a live-found bug: <c>RelationshipAnalyzer.AnalyzeRelationships</c>
/// walked every entity in the model, including owned ones, as a potential relationship source with no
/// <c>IsOwned</c> filter. EF Core owned types can legally declare navigation properties on their CLR
/// class, so an owned type's class referencing back to a root entity fabricated a real
/// <c>EfRelationship</c> and a spurious ERD line — even though no EfRelationship is ever supposed to
/// exist for an owned type. Exercises the full DbContext analysis pipeline (Roslyn compilation,
/// DbSet/OwnsOne discovery, relationship analysis, Mermaid rendering) rather than a hand-built model,
/// so it catches the bug at the same layer the reviewer's live reproduction did. The fixture lives in
/// an isolated <see cref="TestDirectory"/> rather than the shared Golden/fixtures directory, which
/// EntityFileDiscovery scans in full for every golden context — adding a type there risks a duplicate
/// name silently merging with an unrelated fixture's members.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class OwnedNavLeakRegressionTests
{
    private static EfAnalysisService CreateService()
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        return new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
    }

    [Fact]
    public async Task AnalyzeContextAsync_OwnedTypeClrClassHasNavigationToRoot_NoRelationshipOrErdLineForOwnedType()
    {
        using var temp = new TestDirectory();
        const string content = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class OwnedNavLeakContext : DbContext
            {
                public DbSet<Flight> Flights { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Flight>(e =>
                    {
                        e.OwnsOne(f => f.Cabin);
                    });
                }
            }

            public class Flight
            {
                public int Id { get; set; }
                public Cabin Cabin { get; set; } = null!;
            }

            public class Cabin
            {
                public string Layout { get; set; } = "";

                // Legal EF Core owned-type navigation back to the owner's root entity. Nothing in EF
                // Core forbids this; RelationshipAnalyzer must never turn it into a fabricated
                // top-level relationship.
                public Flight ParentFlight { get; set; } = null!;
            }
            """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await CreateService().AnalyzeContextAsync(filePath, "OwnedNavLeakContext");

        // No EfRelationship may name the owned entity, on either side.
        model.Relationships.Should().NotContain(r => r.SourceEntity == "Cabin" || r.TargetEntity == "Cabin");

        var rendered = new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));

        // The reviewer's live reproduction (Ticket/SeatLocation) surfaced the bug as an unqualified,
        // spurious relationship line naming the owned type. Assert neither direction appears here.
        rendered.Should().NotContain("Cabin ||--|| Flight",
            "no EfRelationship is ever fabricated for an owned type's CLR navigation");
        rendered.Should().NotContain("Flight ||--|| Cabin : \"\"",
            "the only Flight/Cabin relationship line allowed is the derived owned-identifying one, " +
            "which carries the navigation name, not an empty label");
    }
}
