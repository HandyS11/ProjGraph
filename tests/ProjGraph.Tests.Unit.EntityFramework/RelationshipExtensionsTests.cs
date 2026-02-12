using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="RelationshipExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RelationshipExtensionsTests
{
    [Fact]
    public void GenerateKey_OneToMany_ShouldPreserveDirection()
    {
        var rel = new EfRelationship
        {
            SourceEntity = "Order",
            TargetEntity = "OrderItem",
            Type = EfRelationshipType.OneToMany
        };

        var key = rel.GenerateKey();

        key.Should().Be("Order-OrderItem-OneToMany");
    }

    [Fact]
    public void GenerateKey_OneToOne_ShouldSortEntitiesAlphabetically()
    {
        var rel = new EfRelationship
        {
            SourceEntity = "Zebra",
            TargetEntity = "Animal",
            Type = EfRelationshipType.OneToOne
        };

        var key = rel.GenerateKey();

        key.Should().Be("Animal-Zebra-OneToOne");
    }

    [Fact]
    public void GenerateKey_ManyToMany_ShouldSortEntitiesAlphabetically()
    {
        var rel = new EfRelationship
        {
            SourceEntity = "Student",
            TargetEntity = "Course",
            Type = EfRelationshipType.ManyToMany
        };

        var key = rel.GenerateKey();

        key.Should().Be("Course-Student-ManyToMany");
    }

    [Fact]
    public void GenerateKey_OneToOne_SameKeyRegardlessOfDirection()
    {
        var rel1 = new EfRelationship
        {
            SourceEntity = "A",
            TargetEntity = "B",
            Type = EfRelationshipType.OneToOne
        };
        var rel2 = new EfRelationship
        {
            SourceEntity = "B",
            TargetEntity = "A",
            Type = EfRelationshipType.OneToOne
        };

        rel1.GenerateKey().Should().Be(rel2.GenerateKey());
    }

    [Fact]
    public void GenerateKey_OneToMany_DifferentKeyForDifferentDirections()
    {
        var rel1 = new EfRelationship
        {
            SourceEntity = "A",
            TargetEntity = "B",
            Type = EfRelationshipType.OneToMany
        };
        var rel2 = new EfRelationship
        {
            SourceEntity = "B",
            TargetEntity = "A",
            Type = EfRelationshipType.OneToMany
        };

        rel1.GenerateKey().Should().NotBe(rel2.GenerateKey());
    }
}
