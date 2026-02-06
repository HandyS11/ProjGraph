using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

[Trait("Category", "ClassDiagram")]
public sealed class ClassAnalysisServiceTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempFile;
    private readonly ClassAnalysisService _service;

    public ClassAnalysisServiceTests()
    {
        _tempFile = Path.Combine(_temp.DirectoryPath, "temp.cs");
        _service = new ClassAnalysisService(new AnalyzeFileUseCase(new CompilationFactory(), new TypeProcessor(),
            new PhysicalFileSystem()));
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temp.Dispose();
        }
    }

    [Fact]
    public async Task AnalyzeFileAsync_SimpleClass_ExtractsBasicInfo()
    {
        const string code = """

                            namespace TestNamespace;

                            public class MyClass {
                                public int MyProperty { get; set; }
                                private void MyMethod() {}
                            }
                            """;
        await File.WriteAllTextAsync(_tempFile, code);

        var result = await _service.AnalyzeFileAsync(_tempFile, false);

        result.Types.Should().HaveCount(1);
        var type = result.Types[0];
        type.Name.Should().Be("MyClass");
        type.Members.Should().HaveCount(2);
        type.Members.Should().Contain(m => m.Name == "MyProperty" && m.Kind == MemberKind.Property);
        type.Members.Should().Contain(m => m.Name == "MyMethod" && m.Kind == MemberKind.Method);
    }

    [Fact]
    public async Task AnalyzeFileAsync_GenericClass_ExtractsGenericName()
    {
        const string code = """
                            namespace TestNamespace;
                            public class MyGeneric<T1, T2> {}
                            """;
        await File.WriteAllTextAsync(_tempFile, code);

        var result = await _service.AnalyzeFileAsync(_tempFile, false);

        result.Types.Should().HaveCount(1);
        var type = result.Types[0];
        type.Name.Should().Be("MyGeneric<T1, T2>");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithInheritance_SingleFile_FindsRelationship()
    {
        const string code = """

                            public class Base {}
                            public class Derived : Base {}
                            """;
        await File.WriteAllTextAsync(_tempFile, code);

        var result = await _service.AnalyzeFileAsync(_tempFile);

        result.Types.Should().HaveCount(2);
        result.Relationships.Should().HaveCount(1);
        var rel = result.Relationships[0];
        rel.From.Should().Be("Derived");
        rel.To.Should().Be("Base");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithWorkspaceDiscovery_FindsRelatedType()
    {
        var root = Path.Combine(_temp.DirectoryPath, "workspace");
        Directory.CreateDirectory(root);

        var serviceFile = Path.Combine(root, "Service.cs");
        var modelFile = Path.Combine(root, "Model.cs");
        await File.WriteAllTextAsync(serviceFile, "public class Service { public Model M { get; } }");
        await File.WriteAllTextAsync(modelFile, "public class Model {}");
        await File.WriteAllTextAsync(Path.Combine(root, "Test.csproj"), "<Project />");

        var result = await _service.AnalyzeFileAsync(serviceFile, false, true);

        result.Types.Should().Contain(t => t.Name == "Service");
        result.Types.Should().Contain(t => t.Name == "Model");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithNamespacedInheritance_UsesFullyQualifiedNames()
    {
        const string code = """

                            namespace MyApp.Models;

                            public class Base {}
                            public class Derived : Base {}
                            """;
        await File.WriteAllTextAsync(_tempFile, code);

        var result = await _service.AnalyzeFileAsync(_tempFile);

        result.Types.Should().HaveCount(2);
        result.Relationships.Should().HaveCount(1);
        var rel = result.Relationships[0];
        rel.From.Should().Be("MyApp.Models.Derived");
        rel.To.Should().Be("MyApp.Models.Base");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithCrossNamespaceInheritance_UsesFullyQualifiedNames()
    {
        var root = Path.Combine(_temp.DirectoryPath, "namespace-test");
        Directory.CreateDirectory(root);

        var baseFile = Path.Combine(root, "BaseEntity.cs");
        var userFile = Path.Combine(root, "User.cs");
        await File.WriteAllTextAsync(baseFile, """
                                               namespace SimpleHierarchy.Base;
                                               public abstract class BaseEntity { public int Id { get; set; } }
                                               """);
        await File.WriteAllTextAsync(userFile, """
                                               using SimpleHierarchy.Base;
                                               namespace SimpleHierarchy.Models;
                                               public class User : BaseEntity { public string Name { get; set; } }
                                               """);
        await File.WriteAllTextAsync(Path.Combine(root, "Test.csproj"), "<Project />");

        var result = await _service.AnalyzeFileAsync(userFile);

        result.Types.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Relationships.Should().HaveCount(1);
        var rel = result.Relationships[0];
        rel.From.Should().Be("SimpleHierarchy.Models.User");
        rel.To.Should().Be("SimpleHierarchy.Base.BaseEntity");
    }

    [Fact]
    public async Task AnalyzeFileAsync_Enum_NoSelfReferencesAndNoTypeInMembers()
    {
        const string code = """
                            namespace SimpleHierarchy.Enums;

                            public enum Types
                            {
                                None = 0,
                                TypeA = 1,
                                TypeB = 2,
                                TypeC = 3
                            }
                            """;
        await File.WriteAllTextAsync(_tempFile, code);

        var result = await _service.AnalyzeFileAsync(_tempFile, false, true);

        result.Types.Should().HaveCount(1);
        var type = result.Types[0];
        type.Name.Should().Be("Types");
        type.Kind.Should().Be(TypeKind.Enum);
        type.Members.Should().HaveCount(4);

        // Enum members should have empty type strings
        type.Members.Should().OnlyContain(m => string.IsNullOrEmpty(m.Type));
        type.Members.Should().Contain(m => m.Name == "None");
        type.Members.Should().Contain(m => m.Name == "TypeA");
        type.Members.Should().Contain(m => m.Name == "TypeB");
        type.Members.Should().Contain(m => m.Name == "TypeC");

        // Enum should not have any relationships to itself
        result.Relationships.Should().BeEmpty();
    }
}
