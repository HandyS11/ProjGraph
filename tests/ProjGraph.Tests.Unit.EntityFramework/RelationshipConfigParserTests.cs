using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="RelationshipConfigParser"/> chain-boundary handling: configuration from
/// one fluent chain must never leak into an unrelated chain in the same entity section.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class RelationshipConfigParserTests
{
    private static Dictionary<string, EfEntity> CreateEntities(params string[] names)
    {
        return names.ToDictionary(n => n, n => new EfEntity { Name = n });
    }

    [Fact]
    public void ParseExplicitRelationships_IsRequiredOnLaterPropertyChain_ShouldNotMarkRelationshipRequired()
    {
        // The .IsRequired() belongs to the Property(...) chain that follows the relationship
        // statement; the scan must stop at that chain boundary instead of associating it
        // with the one-to-one relationship.
        const string configSection = """
                                     entity.HasOne(a => a.Profile).WithOne(p => p.Author);
                                     entity.Property(a => a.Name).IsRequired();
                                     """;
        var entities = CreateEntities("Author", "Profile");
        var relationships = new List<EfRelationship>();
        var compilation = RoslynTestHelper.CreateCompilation("namespace Empty;");

        RelationshipConfigParser.ParseExplicitRelationships(
            configSection, "Author", entities, relationships, compilation);

        var relationship = relationships.Should().ContainSingle().Which;
        relationship.Type.Should().Be(EfRelationshipType.OneToOne);
        relationship.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ParseExplicitRelationships_HasOneWithoutWith_ShouldNotPairWithNextChain()
    {
        // The bare HasOne has no WithOne/WithMany in its own chain; it must not borrow the
        // WithOne from the following HasMany chain and fabricate a one-to-one relationship.
        const string configSection = """
                                     entity.HasOne(b => b.Owner);
                                     entity.HasMany(b => b.Post).WithOne(p => p.Blog);
                                     """;
        var entities = CreateEntities("Blog", "Post", "Owner");
        var relationships = new List<EfRelationship>();
        var compilation = RoslynTestHelper.CreateCompilation("namespace Empty;");

        RelationshipConfigParser.ParseExplicitRelationships(
            configSection, "Blog", entities, relationships, compilation);

        var relationship = relationships.Should().ContainSingle().Which;
        relationship.Type.Should().Be(EfRelationshipType.OneToMany);
        relationships.Should().NotContain(r => r.Type == EfRelationshipType.OneToOne);
    }

    [Fact]
    public void ParseExplicitRelationships_IsRequiredInSameChain_ShouldMarkRelationshipRequired()
    {
        // An .IsRequired() inside the relationship's own chain (after HasForeignKey) must
        // still be honored — chain-boundary detection must not cut the chain short.
        const string configSection = """
                                     entity.HasOne(p => p.Author).WithOne(a => a.Profile).HasForeignKey(p => p.AuthorId).IsRequired();
                                     """;
        var entities = CreateEntities("Author", "Profile");
        var relationships = new List<EfRelationship>();
        var compilation = RoslynTestHelper.CreateCompilation("namespace Empty;");

        RelationshipConfigParser.ParseExplicitRelationships(
            configSection, "Profile", entities, relationships, compilation);

        var relationship = relationships.Should().ContainSingle().Which;
        relationship.Type.Should().Be(EfRelationshipType.OneToOne);
        relationship.IsRequired.Should().BeTrue();
    }
}
