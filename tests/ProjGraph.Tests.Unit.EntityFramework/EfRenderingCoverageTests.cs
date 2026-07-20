using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Rendering;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Edge-case coverage for <see cref="MermaidErdRenderer"/>: rendering fallbacks for models that carry
/// values the happy path never produces (blank column types, unknown relationship kinds, an owned type
/// whose owner is missing from the model, and an ownership cycle).
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class EfRenderingCoverageTests
{
    private readonly MermaidErdRenderer _renderer = new();

    [Fact]
    public void Format_ShouldBeMermaid()
    {
        _renderer.Format.Should().Be("mermaid");
    }

    [Fact]
    public void Render_PropertyWithBlankType_ShouldFallBackToUnknown()
    {
        // A property whose type could not be resolved must still produce a syntactically valid Mermaid
        // column line: "  Legacy" (no type token) is not valid ER syntax, "unknown Legacy" is.
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity
                {
                    Name = "Legacy",
                    Properties =
                    [
                        new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                        new EfProperty { Name = "Payload", Type = "" }
                    ]
                }
            ]
        };

        var result = _renderer.Render(model);

        result.Should().Contain("unknown Payload");
    }

    [Fact]
    public void Render_PropertyWhoseTypeSanitizesToNothing_ShouldFallBackToUnknown()
    {
        // "?" survives as a non-empty Type but sanitizes away entirely, so the fallback has to be applied
        // after sanitization rather than on the raw value.
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity
                {
                    Name = "Legacy",
                    Properties = [new EfProperty { Name = "Payload", Type = "?" }]
                }
            ]
        };

        var result = _renderer.Render(model);

        result.Should().Contain("unknown Payload");
    }

    [Fact]
    public void Render_UnrecognizedRelationshipType_ShouldEmitNeutralConnector()
    {
        // Guards the switch's default arm: an out-of-range enum value must degrade to a plain link
        // instead of throwing and taking the whole diagram down.
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity { Name = "Alpha" },
                new EfEntity { Name = "Beta" }
            ],
            Relationships =
            [
                new EfRelationship
                {
                    SourceEntity = "Alpha",
                    TargetEntity = "Beta",
                    Type = (EfRelationshipType)99
                }
            ]
        };

        var result = _renderer.Render(model);

        result.Should().Contain("Alpha -- Beta : \"\"");
    }

    [Fact]
    public void Render_PrecisionWithoutScale_ShouldRenderPrecisionOnly()
    {
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity
                {
                    Name = "Product",
                    Properties = [new EfProperty { Name = "Weight", Type = "decimal", Precision = 10 }]
                }
            ]
        };

        var result = _renderer.Render(model);

        result.Should().Contain("precision:10");
        result.Should().NotContain("precision(");
    }

    [Fact]
    public void Render_OwnedEntityWithUnresolvableOwner_ShouldStillDrawBoxButNoOwnershipLine()
    {
        // An owned entity whose OwnerEntity names no entity in the model cannot be inlined (there is no
        // owner table to compare against) and cannot get an identifying relationship line either — but it
        // must not be dropped, or its columns disappear from the diagram with no trace.
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity
                {
                    Name = "Address",
                    Key = "Ghost.ShipTo",
                    IsOwned = true,
                    OwnerEntity = "Ghost",
                    NavigationName = "ShipTo",
                    Properties = [new EfProperty { Name = "Street", Type = "string" }]
                }
            ]
        };

        var result = _renderer.Render(model);

        result.Should().Contain("Address {");
        result.Should().Contain("string Street");
        result.Should().NotContain("||--||");
        result.Should().NotContain("Ghost");
    }

    [Fact]
    public void Render_EntityNameThatIsNotABareIdentifier_ShouldBeQuoted()
    {
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities = [new EfEntity { Name = "Order Detail" }]
        };

        var result = _renderer.Render(model);

        result.Should().Contain("\"Order Detail\" {");
    }

    [Fact]
    public void Render_OwnedEntitiesSharingAKey_ShouldTerminateAndInlineEachOnce()
    {
        // Two owned entities that share an EffectiveKey form a cycle in the owner graph: the inner one's
        // children resolve back to the key already being expanded. Without the recursion guard this
        // recurses forever and dies with an uncatchable StackOverflowException. The guard must stop the
        // second expansion of the key while still emitting everything reached before it.
        var model = new EfModel
        {
            ContextName = "TestDbContext",
            Entities =
            [
                new EfEntity
                {
                    Name = "Invoice",
                    TableName = "Invoices",
                    Properties = [new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true }]
                },
                new EfEntity
                {
                    Name = "Address",
                    Key = "Invoice.ShipTo",
                    TableName = "Invoices",
                    IsOwned = true,
                    OwnerEntity = "Invoice",
                    NavigationName = "ShipTo",
                    Properties = [new EfProperty { Name = "Street", Type = "string" }]
                },
                new EfEntity
                {
                    // Deliberately duplicates the key above, so its own children lookup ("who is owned by
                    // Invoice.ShipTo?") finds itself and loops.
                    Name = "Geo",
                    Key = "Invoice.ShipTo",
                    TableName = "Invoices",
                    IsOwned = true,
                    OwnerEntity = "Invoice.ShipTo",
                    NavigationName = "Geo",
                    Properties = [new EfProperty { Name = "Lat", Type = "double" }]
                }
            ]
        };

        var result = _renderer.Render(model);

        // Everything folds onto the single Invoices table, with EF's compounding column prefixes.
        result.Should().Contain("Invoice {");
        result.Should().Contain("ShipTo_Street");
        result.Should().Contain("ShipTo_Geo_Lat");
        result.Should().NotContain("Address {");
        result.Should().NotContain("Geo {");
    }
}
