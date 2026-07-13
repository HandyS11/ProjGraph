using Microsoft.CodeAnalysis;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="TypeFilter"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TypeFilterTests
{
    [Fact]
    public void IsSystemType_SystemNamespace_ShouldReturnTrue()
    {
        const string code = """
                            namespace Test;
                            public class Holder
                            {
                                public System.Collections.Generic.List<int> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Holder")!;
        var propType = (INamedTypeSymbol)symbol.GetMembers().OfType<IPropertySymbol>()
            .First(p => p.Name == "Items").Type;

        TypeFilter.IsSystemType(propType).Should().BeTrue();
    }

    [Fact]
    public void IsSystemType_UserType_ShouldReturnFalse()
    {
        const string code = """
                            namespace MyApp;
                            public class UserClass { }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "UserClass")!;

        TypeFilter.IsSystemType(symbol).Should().BeFalse();
    }

    [Fact]
    public void IsSystemType_UserTypeWithWellKnownName_ShouldReturnFalse()
    {
        // A user-defined domain type named 'Task' living in a real namespace must NOT be
        // filtered out just because its simple name collides with a BCL type name.
        const string code = """
                            namespace MyApp;
                            public class Task { public int Id { get; set; } }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Task")!;

        TypeFilter.IsSystemType(symbol).Should().BeFalse();
    }

    [Fact]
    public void IsSystemType_UserNamespaceStartingWithSystem_ShouldReturnFalse()
    {
        // 'Systems.Combat' is a user namespace, not the BCL 'System' namespace; a prefix
        // check without a dot boundary would wrongly classify its types as system types.
        const string code = """
                            namespace Systems;
                            public class Weapon { public int Damage { get; set; } }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Weapon")!;

        TypeFilter.IsSystemType(symbol).Should().BeFalse();
    }

    [Fact]
    public void IsSystemType_SpecialType_ShouldReturnTrue()
    {
        const string code = """
                            namespace Test;
                            public class Holder
                            {
                                public string Value { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Holder")!;
        var propType = (INamedTypeSymbol)symbol.GetMembers().OfType<IPropertySymbol>()
            .First(p => p.Name == "Value").Type;

        TypeFilter.IsSystemType(propType).Should().BeTrue();
    }
}
