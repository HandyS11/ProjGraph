using FluentAssertions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.ClassAnalysis;

namespace ProjGraph.Tests.Unit.Services.ClassAnalysis;

public class ClassAnalysisServiceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName() + ".cs";
    private readonly ClassAnalysisService _service = new();

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }

        GC.SuppressFinalize(this);
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
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            var serviceFile = Path.Combine(root, "Service.cs");
            var modelFile = Path.Combine(root, "Model.cs");
            await File.WriteAllTextAsync(serviceFile, "public class Service { public Model M { get; } }");
            await File.WriteAllTextAsync(modelFile, "public class Model {}");
            await File.WriteAllTextAsync(Path.Combine(root, "Test.csproj"), "<Project />");

            var result = await _service.AnalyzeFileAsync(serviceFile, false, true);

            result.Types.Should().Contain(t => t.Name == "Service");
            result.Types.Should().Contain(t => t.Name == "Model");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}