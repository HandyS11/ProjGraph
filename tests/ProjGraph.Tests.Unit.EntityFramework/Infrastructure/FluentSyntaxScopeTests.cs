using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class FluentSyntaxScopeTests
{
    private const string Source = """
        class C
        {
            void OnModelCreating(object modelBuilder)
            {
                modelBuilder.Entity<Order>(e =>
                {
                    e.Property(o => o.Total);
                    e.OwnsOne(o => o.ShipToAddress, a =>
                    {
                        a.Property(x => x.City);
                    });
                });
            }
        }
        """;

    private static MethodDeclarationSyntax Method() =>
        CSharpSyntaxTree.ParseText(Source).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

    private static InvocationExpressionSyntax OwnsOneLambdaBody(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(i => i.Expression is MemberAccessExpressionSyntax ma
                         && ma.Name.Identifier.Text == "OwnsOne");
    }

    [Fact]
    public void FindConfigRoots_AtMethodScope_ExcludesPropertiesInsideOwnedBuilder()
    {
        var method = Method();

        var roots = FluentSyntax.FindConfigRoots(method, "Property").ToList();

        roots.Should().HaveCount(1, "the City property inside the OwnsOne builder must stay fenced off");
        roots[0].ToString().Should().Contain("o.Total");
    }

    [Fact]
    public void FindConfigRoots_ScopedToOwnedBuilder_FindsOnlyItsOwnProperties()
    {
        var method = Method();
        var ownsOne = OwnsOneLambdaBody(method);

        var roots = FluentSyntax.FindConfigRoots(ownsOne.ArgumentList, "Property").ToList();

        roots.Should().HaveCount(1, "scoping to the owned builder must surface its own Property calls");
        roots[0].ToString().Should().Contain("x.City");
    }
}
