using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="DbContextIdentifier"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DbContextIdentifierTests
{
    private static ClassDeclarationSyntax ParseClass(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
    }

    private static IEnumerable<ClassDeclarationSyntax> ParseClasses(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
    }

    [Fact]
    public void IsDbContext_ClassDerivesFromDbContext_ShouldReturnTrue()
    {
        var classDecl = ParseClass("public class MyContext : DbContext { }");

        DbContextIdentifier.IsDbContext(classDecl).Should().BeTrue();
    }

    [Fact]
    public void IsDbContext_ClassDoesNotDeriveFromDbContext_ShouldReturnFalse()
    {
        var classDecl = ParseClass("public class MyService : BaseService { }");

        DbContextIdentifier.IsDbContext(classDecl).Should().BeFalse();
    }

    [Fact]
    public void IsDbContext_ClassWithNoBaseList_ShouldReturnFalse()
    {
        var classDecl = ParseClass("public class MyClass { }");

        DbContextIdentifier.IsDbContext(classDecl).Should().BeFalse();
    }

    [Fact]
    public void IsModelSnapshot_ClassDerivesFromModelSnapshot_ShouldReturnTrue()
    {
        var classDecl = ParseClass("public class MyModelSnapshot : ModelSnapshot { }");

        DbContextIdentifier.IsModelSnapshot(classDecl).Should().BeTrue();
    }

    [Fact]
    public void IsModelSnapshot_ClassDoesNotInheritModelSnapshot_ShouldReturnFalse()
    {
        var classDecl = ParseClass("public class MyContext : DbContext { }");

        DbContextIdentifier.IsModelSnapshot(classDecl).Should().BeFalse();
    }

    [Fact]
    public void FindContextClass_WithMatchingName_ShouldReturnCorrectClass()
    {
        var classes = ParseClasses("""
                                   public class OtherClass { }
                                   public class MyContext : DbContext { }
                                   public class Service { }
                                   """);

        var result = DbContextIdentifier.FindContextClass(classes, "MyContext");

        result.Should().NotBeNull();
        result.Identifier.Text.Should().Be("MyContext");
    }

    [Fact]
    public void FindContextClass_WithNullName_ShouldReturnFirstDbContext()
    {
        var classes = ParseClasses("""
                                   public class OtherClass { }
                                   public class MyContext : DbContext { }
                                   """);

        var result = DbContextIdentifier.FindContextClass(classes, null);

        result.Should().NotBeNull();
        result.Identifier.Text.Should().Be("MyContext");
    }

    [Fact]
    public void FindContextClass_NoMatch_ShouldReturnNull()
    {
        var classes = ParseClasses("public class OtherClass { }");

        var result = DbContextIdentifier.FindContextClass(classes, "Missing");

        result.Should().BeNull();
    }

    [Fact]
    public void FindSnapshotClass_WithMatchingName_ShouldReturnCorrectClass()
    {
        var classes = ParseClasses("""
                                   public class OtherClass { }
                                   public class AppModelSnapshot : ModelSnapshot { }
                                   """);

        var result = DbContextIdentifier.FindSnapshotClass(classes, "AppModelSnapshot");

        result.Should().NotBeNull();
        result.Identifier.Text.Should().Be("AppModelSnapshot");
    }

    [Fact]
    public void FindSnapshotClass_WithNullName_ShouldReturnFirstSnapshot()
    {
        var classes = ParseClasses("""
                                   public class OtherClass { }
                                   public class AppModelSnapshot : ModelSnapshot { }
                                   """);

        var result = DbContextIdentifier.FindSnapshotClass(classes, null);

        result.Should().NotBeNull();
        result.Identifier.Text.Should().Be("AppModelSnapshot");
    }
}
