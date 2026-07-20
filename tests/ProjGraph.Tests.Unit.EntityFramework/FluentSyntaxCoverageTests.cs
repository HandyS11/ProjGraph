using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Edge-case unit tests for <see cref="FluentSyntax"/>, the shared syntax-walking primitives behind every
/// Fluent API walker: receiver-chain / ancestor owning-entity resolution across owned-type and join-entity
/// fences, the unusual fluent chain shapes whose name extraction must degrade to <see langword="null"/>
/// rather than guess, and entity materialization when a type symbol cannot be resolved.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentSyntaxCoverageTests
{
    /// <summary>Parses <paramref name="source"/> and returns its single method declaration.</summary>
    /// <param name="source">The C# source to parse.</param>
    private static MethodDeclarationSyntax ParseMethod(string source)
        => CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

    /// <summary>
    /// Returns the outermost invocation in <paramref name="scope"/> whose immediate member name is
    /// <paramref name="methodName"/> (pre-order traversal yields the outermost chain link first).
    /// </summary>
    /// <param name="scope">The node to search.</param>
    /// <param name="methodName">The simple method name to match.</param>
    private static InvocationExpressionSyntax Call(SyntaxNode scope, string methodName)
        => scope.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .First(i => i.Expression is MemberAccessExpressionSyntax ma
                        && ma.Name.Identifier.Text == methodName);

    /// <summary>Wraps <paramref name="body"/> in a minimal OnModelCreating method and parses it.</summary>
    /// <param name="body">The statements to place inside the method body.</param>
    private static MethodDeclarationSyntax Method(string body)
        => ParseMethod("class Ctx { void OnModelCreating(dynamic modelBuilder) { " + body + " } }");

    [Fact]
    public void ResolveOwningEntity_ReceiverChainLinkIsNotMemberAccess_FallsBackToAmbient()
    {
        // The chain's receiver is a bare `Factory()` call, not a member access, so the receiver walk has
        // no name to inspect and must keep climbing rather than abandon resolution.
        var method = Method("Factory().Property(a => a.Name);");

        var resolved = FluentSyntax.ResolveOwningEntity(Call(method, "Property"), "Account");

        resolved.Should().Be("Account");
    }

    [Fact]
    public void ResolveOwningEntity_ChainCrossesUsingEntity_ReturnsNull()
    {
        // A join entity is out of scope: a ToTable chained onto UsingEntity must NOT be attributed to the
        // Post entity the chain would otherwise reach, nor to the ambient entity.
        var method = Method("""modelBuilder.Entity<Post>().UsingEntity("PostTag").ToTable("post_tag");""");

        var resolved = FluentSyntax.ResolveOwningEntity(Call(method, "ToTable"), "Post");

        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveOwningEntity_ChainCrossesOwnsOne_ResolvesToOwnedKey()
    {
        var method = Method("""modelBuilder.Entity<Order>().OwnsOne(o => o.ShipTo).ToTable("ship_to");""");

        var resolved = FluentSyntax.ResolveOwningEntity(Call(method, "ToTable"), null);

        resolved.Should().Be("Order.ShipTo");
    }

    [Fact]
    public void ResolveOwningEntity_ChainCrossesOwnsOneWithUnresolvableNavigation_ReturnsNull()
    {
        // The owned navigation cannot be named (the argument is neither a lambda member access nor a pair
        // of string literals), so the chain settles on "unresolvable" rather than leaking onto Order.
        var method = Method("modelBuilder.Entity<Order>().OwnsOne(AddressConfig).Property(a => a.City);");

        var resolved = FluentSyntax.ResolveOwningEntity(Call(method, "Property"), "Order");

        resolved.Should().BeNull("an unnameable owned target must not fall back to its owner");
    }

    [Fact]
    public void ResolveOwningEntity_InsideOwnedBuilderLambda_StopsAtFenceAndKeepsAmbient()
    {
        // The ancestor search would otherwise climb past the OwnsOne fence and reattribute the nested
        // Property call to Entity<Order>(). The ambient owned key must win instead.
        var method = Method("modelBuilder.Entity<Order>(e => e.OwnsOne(o => o.ShipTo, a => a.Property(x => x.City)));");

        var resolved = FluentSyntax.ResolveOwningEntity(Call(method, "Property"), "Order.ShipTo");

        resolved.Should().Be("Order.ShipTo");
    }

    [Fact]
    public void ResolveOwningEntity_InsideUsingEntityLambda_KeepsAmbient()
    {
        var method = Method("""modelBuilder.Entity<Post>(e => e.HasMany(p => p.Tags).WithMany().UsingEntity("PostTag", j => j.Property<int>("PostId")));""");

        var resolved = FluentSyntax.ResolveOwningEntity(Call(method, "Property"), "PostTag");

        resolved.Should().Be("PostTag", "a join-entity builder's own configuration stays on the join entity");
    }

    [Fact]
    public void FindConfigRoots_CallsInsideUsingEntityArgumentList_AreExcluded()
    {
        var method = Method("""modelBuilder.Entity<Post>().HasMany(p => p.Tags).WithMany().UsingEntity("PostTag", j => j.Property<int>("PostId"));""");

        var roots = FluentSyntax.FindConfigRoots(method, "Property").ToList();

        roots.Should().BeEmpty("join-entity builder configuration must not be walked as the outer entity's");
    }

    [Fact]
    public void IsInsideNestedBuilderScope_NodeOnTheFenceReceiverSpine_IsNotFenced()
    {
        // The Entity<Order>() call is the OwnsOne call's receiver, not one of its arguments, so it must
        // not be treated as fenced off by the very call chained onto it.
        var method = Method("modelBuilder.Entity<Order>().OwnsOne(o => o.ShipTo, a => a.Property(x => x.City));");
        var entityCall = Call(method, "Entity");

        FluentSyntax.IsInsideNestedBuilderScope(entityCall, method).Should().BeFalse();
        FluentSyntax.IsInsideNestedBuilderScope(Call(method, "Property"), method).Should().BeTrue();
    }

    [Fact]
    public void OwnedNavigationName_NoArguments_ReturnsNull()
    {
        var method = Method("modelBuilder.Entity<Order>().OwnsOne();");

        FluentSyntax.OwnedNavigationName(Call(method, "OwnsOne")).Should().BeNull();
    }

    [Fact]
    public void OwnedNavigationName_ParenthesizedLambda_ReturnsMemberName()
    {
        var method = Method("modelBuilder.Entity<Order>().OwnsOne((o) => o.ShipTo, a => a.Property(x => x.City));");

        FluentSyntax.OwnedNavigationName(Call(method, "OwnsOne")).Should().Be("ShipTo");
    }

    [Fact]
    public void OwnedNavigationName_SnapshotStringLiteralForm_ReturnsSecondLiteral()
    {
        var method = Method("""b.OwnsOne("Fixtures.Address", "ShipTo", b1 => b1.Property<string>("City"));""");

        FluentSyntax.OwnedNavigationName(Call(method, "OwnsOne")).Should().Be("ShipTo",
            "the snapshot form names the owned TYPE first and the navigation second");
    }

    [Fact]
    public void OwnedNavigationName_NonLambdaNonLiteralArgument_ReturnsNull()
    {
        var method = Method("modelBuilder.Entity<Order>().OwnsOne(AddressConfig, a => a.Property(x => x.City));");

        FluentSyntax.OwnedNavigationName(Call(method, "OwnsOne")).Should().BeNull();
    }

    [Fact]
    public void EntityNameFromInvocation_QualifiedGenericArgument_StripsNamespace()
    {
        var method = Method("modelBuilder.Entity<Data.Models.Order>();");

        FluentSyntax.EntityNameFromInvocation(Call(method, "Entity")).Should().Be("Order");
    }

    [Fact]
    public void EntityNameFromInvocation_UnqualifiedStringLiteral_ReturnsWholeLiteral()
    {
        var method = Method("""modelBuilder.Entity("Order");""");

        FluentSyntax.EntityNameFromInvocation(Call(method, "Entity")).Should().Be("Order");
    }

    [Fact]
    public void EntityNameFromInvocation_NonLiteralArgument_ReturnsNull()
    {
        var method = Method("modelBuilder.Entity(typeof(Order));");

        FluentSyntax.EntityNameFromInvocation(Call(method, "Entity")).Should().BeNull(
            "a typeof argument is not a name this syntax-only walker can read");
    }

    [Fact]
    public void GenericTypeArgumentName_NonGenericInvocation_ReturnsNull()
    {
        var method = Method("""modelBuilder.Entity("Ns.Order");""");

        FluentSyntax.GenericTypeArgumentName(Call(method, "Entity")).Should().BeNull();
    }

    [Fact]
    public void MaterializeEntity_UnresolvableTypeName_AddsBareEntity()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Present { public int Id { get; set; } }");
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();

        FluentSyntax.MaterializeEntity("Absent", entities, model, compilation);

        entities.Should().ContainKey("Absent");
        entities["Absent"].Name.Should().Be("Absent");
        entities["Absent"].Properties.Should().BeEmpty("an unresolvable type degrades to a bare entity");
        model.Entities.Should().ContainSingle(e => e.Name == "Absent");
    }

    [Fact]
    public void MaterializeEntity_ResolvableTypeName_SeedsPropertiesFromSymbol()
    {
        var compilation = RoslynTestHelper.CreateCompilation(
            "public class Present { public int Id { get; set; } public string Label { get; set; } = \"\"; }");
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();

        FluentSyntax.MaterializeEntity("Present", entities, model, compilation);

        entities["Present"].Properties.Select(p => p.Name).Should().Contain("Id").And.Contain("Label");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaterializeEntity_NullOrEmptyName_DoesNothing(string? entityName)
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Present { public int Id { get; set; } }");
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();

        FluentSyntax.MaterializeEntity(entityName, entities, model, compilation);

        entities.Should().BeEmpty();
        model.Entities.Should().BeEmpty();
    }

    [Fact]
    public void MaterializeEntity_AlreadyKnownEntity_PreservesTheExistingInstance()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Present { public int Id { get; set; } }");
        var existing = new EfEntity { Name = "Present", TableName = "presents" };
        var entities = new Dictionary<string, EfEntity> { ["Present"] = existing };
        var model = new EfModel();
        model.Entities.Add(existing);

        FluentSyntax.MaterializeEntity("Present", entities, model, compilation);

        entities["Present"].Should().BeSameAs(existing,
            "re-materializing must not discard configuration already applied to the entity");
        model.Entities.Should().ContainSingle();
    }

    [Fact]
    public void MaterializeEntity_NameAlreadyInModelButNotInDictionary_DoesNotDuplicateInModel()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Present { public int Id { get; set; } }");
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();
        model.Entities.Add(new EfEntity { Name = "Present" });

        FluentSyntax.MaterializeEntity("Present", entities, model, compilation);

        entities.Should().ContainKey("Present");
        model.Entities.Should().ContainSingle(e => e.Name == "Present",
            "the model already carried this entity, so it must not be added a second time");
    }

    [Fact]
    public void LastSegment_ValueWithoutDot_ReturnsWholeValue()
    {
        FluentSyntax.LastSegment("Order").Should().Be("Order");
        FluentSyntax.LastSegment("Data.Models.Order").Should().Be("Order");
    }
}
