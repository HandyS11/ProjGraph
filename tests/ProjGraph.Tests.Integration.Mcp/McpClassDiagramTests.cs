using ModelContextProtocol;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

[Collection("McpClassDiagram")]
public sealed class McpClassDiagramTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempFile;
    private readonly string _tempFileWithInheritance;
    private readonly string _tempFileWithDependencies;

    public McpClassDiagramTests()
    {
        var tempDir =
            // Create a dedicated test directory with a marker file to act as workspace root
            _temp.DirectoryPath;

        // Create a dummy .csproj file to mark this as a workspace root
        var csprojPath = Path.Combine(tempDir, "Test.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        // Simple class without dependencies
        _tempFile = Path.Combine(tempDir, "Person.cs");
        const string simpleContent = """
                                     namespace TestNamespace;

                                     public class Person
                                     {
                                         public int Id { get; set; }
                                         public string Name { get; set; }
                                         public int Age { get; set; }
                                     }

                                     public class Product
                                     {
                                         public int ProductId { get; set; }
                                         public string ProductName { get; set; }
                                         public decimal Price { get; set; }
                                     }
                                     """;
        File.WriteAllText(_tempFile, simpleContent);

        // Class with inheritance
        _tempFileWithInheritance = Path.Combine(tempDir, "Inheritance.cs");
        const string inheritanceContent = """
                                          namespace TestNamespace;

                                          public abstract class Entity
                                          {
                                              public int Id { get; set; }
                                              public DateTime CreatedAt { get; set; }
                                          }

                                          public interface INameable
                                          {
                                              string Name { get; set; }
                                          }

                                          public class User : Entity, INameable
                                          {
                                              public string Name { get; set; }
                                              public string Email { get; set; }
                                          }

                                          public class Admin : User
                                          {
                                              public string[] Permissions { get; set; }
                                          }
                                          """;
        File.WriteAllText(_tempFileWithInheritance, inheritanceContent);

        // Class with property dependencies
        _tempFileWithDependencies = Path.Combine(tempDir, "Dependencies.cs");
        const string dependenciesContent = """
                                           using System.Collections.Generic;

                                           namespace TestNamespace;

                                           public class Address
                                           {
                                               public string Street { get; set; }
                                               public string City { get; set; }
                                           }

                                           public class Customer
                                           {
                                               public int Id { get; set; }
                                               public string Name { get; set; }
                                               public Address HomeAddress { get; set; }
                                               public List<Order> Orders { get; set; }
                                           }

                                           public class Order
                                           {
                                               public int OrderId { get; set; }
                                               public DateTime OrderDate { get; set; }
                                               public Customer Customer { get; set; }
                                           }
                                           """;
        File.WriteAllText(_tempFileWithDependencies, dependenciesContent);
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

    private static ProjGraphTools CreateTools()
    {
        return McpTestHelper.CreateTools();
    }


    [Fact]
    public async Task GetClassDiagram_SimpleClasses_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFile);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        result.Should().Contain("class TestNamespace_Person");
        result.Should().Contain("class TestNamespace_Product");
    }

    [Fact]
    public async Task GetClassDiagram_SimpleClasses_ShouldShowProperties()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFile);

        // Assert
        result.Should().Contain("int Id");
        result.Should().Contain("string Name");
        result.Should().Contain("int Age");
        result.Should().Contain("int ProductId");
        result.Should().Contain("string ProductName");
        result.Should().Contain("decimal Price");
    }

    [Fact]
    public async Task GetClassDiagram_NonExistentFile_ShouldThrow()
    {
        // Arrange
        var tools = CreateTools();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "this", "path", "does", "not", "exist.cs");

        // Act
        var act = async () => await tools.GetClassDiagramAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetClassDiagram_NonCsFile_ShouldThrowMcpException()
    {
        // Arrange
        using var temp = new TestDirectory();
        var tools = CreateTools();
        var nonCsFile = temp.CreateFile("notes.txt", "not C# source");

        // Act
        var act = async () => await tools.GetClassDiagramAsync(nonCsFile);

        // Assert - McpException so the guidance reaches the client instead of a generic error
        (await act.Should().ThrowAsync<McpException>()).Which.Message.Should().Contain(".cs");
    }


    [Fact]
    public async Task GetClassDiagram_WithInheritance_ShouldShowBaseClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result =
            await tools.GetClassDiagramAsync(_tempFileWithInheritance, new AnalysisOptions(IncludeInheritance: true));

        // Assert
        result.Should().Contain("class TestNamespace_Entity");
        result.Should().Contain("class TestNamespace_User");
        result.Should().Contain("class TestNamespace_Admin");
        result.Should().Contain("<<abstract>> TestNamespace_Entity", "Entity should be marked as abstract");
        result.Should().Contain("TestNamespace_Entity <|-- TestNamespace_User", "User should inherit from Entity");
        result.Should().Contain("TestNamespace_User <|-- TestNamespace_Admin", "Admin should inherit from User");
    }

    [Fact]
    public async Task GetClassDiagram_WithInheritance_ShouldShowInterfaces()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result =
            await tools.GetClassDiagramAsync(_tempFileWithInheritance, new AnalysisOptions(IncludeInheritance: true));

        // Assert
        result.Should().Contain("class TestNamespace_INameable");
        result.Should().Contain("<<interface>> TestNamespace_INameable");
        result.Should().Contain("TestNamespace_INameable <|.. TestNamespace_User");
    }

    [Fact]
    public async Task GetClassDiagram_WithoutInheritance_ShouldNotShowBaseClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFileWithInheritance);

        // Assert
        result.Should().Contain("class TestNamespace_User");
        result.Should().Contain("class TestNamespace_Admin");
        // Should not show inheritance relationships when disabled
        result.Should().NotContain("<|--");
    }


    [Fact]
    public async Task GetClassDiagram_WithDependencies_ShouldShowRelatedClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result =
            await tools.GetClassDiagramAsync(_tempFileWithDependencies, new AnalysisOptions(IncludeDependencies: true));

        // Assert
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Address");
        result.Should().Contain("class TestNamespace_Order");
        result.Should().Contain("-->", "Should contain association relationships");
    }

    [Fact]
    public async Task GetClassDiagram_WithoutDependencies_ShouldNotShowRelatedClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFileWithDependencies,
            new AnalysisOptions(IncludeDependencies: false));

        // Assert
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Address");
        result.Should().Contain("class TestNamespace_Order");
        // Should not show dependency/association relationships when disabled
        result.Should().NotContain("-->");
    }

    [Fact]
    public async Task GetClassDiagram_WithDepth1_ShouldLimitRelationshipDepth()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFileWithDependencies,
            new AnalysisOptions(IncludeDependencies: true));

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        // With depth 1, should include immediate dependencies
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Address");
    }

    [Fact]
    public async Task GetClassDiagram_WithDepth2_ShouldFollowDeeperRelationships()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFileWithDependencies,
            new AnalysisOptions(2, IncludeDependencies: true));

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Order");
    }


    [Fact]
    public async Task GetClassDiagram_RealProject_CoreModels_ShouldGenerateDiagram()
    {
        // Arrange
        var tools = CreateTools();
        var modelsPath = GetProjectPath(@"src\ProjGraph.Core\Models\ClassDiagramModels.cs");

        // Skip if file doesn't exist (e.g., in CI environment)
        if (!File.Exists(modelsPath))
        {
            throw new SkipTestException($"Sample file not found at: {modelsPath}");
        }

        // Act
        var result = await tools.GetClassDiagramAsync(modelsPath, new AnalysisOptions(IncludeInheritance: true));

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
    }


    [Fact]
    public async Task GetClassDiagram_AllOptionsEnabled_ShouldWorkCorrectly()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(
            _tempFileWithInheritance,
            new AnalysisOptions(3, true, true));

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        result.Should().Contain("class TestNamespace_User");
    }

    [Fact]
    public async Task GetClassDiagram_AllOptionsDisabled_ShouldShowBasicClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(
            _tempFileWithInheritance,
            new AnalysisOptions(0, false, false, false, false));

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        result.Should().Contain("class TestNamespace_User");
        result.Should().Contain("class TestNamespace_Admin");
    }

    [Fact]
    public async Task GetClassDiagram_IncludePropertiesFalse_ShouldExcludeProperties()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagramAsync(_tempFile, new AnalysisOptions(IncludeProperties: false));

        // Assert
        result.Should().NotContain("int Id");
        result.Should().NotContain("string Name");
        result.Should().NotContain("int Age");
    }

    [Fact]
    public async Task GetClassDiagram_IncludeFunctionsFalse_ShouldExcludeMethods()
    {
        // Arrange
        var tools = CreateTools();
        const string fileWithMethods = "Svc.cs";
        var path = Path.Combine(_temp.DirectoryPath, fileWithMethods);

        await File.WriteAllTextAsync(path, "namespace Test; public class Svc { public void DoWork() {} }");

        // Act
        var result = await tools.GetClassDiagramAsync(path, new AnalysisOptions(IncludeFunctions: false));

        // Assert
        result.Should().NotContain("DoWork()");
    }

    [Fact]
    public async Task GetClassDiagram_HiddenMembers_ShouldStillShowRelationships()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        // Hide both properties and functions, but enable inheritance and dependencies
        var result = await tools.GetClassDiagramAsync(
            _tempFileWithDependencies,
            new AnalysisOptions(IncludeInheritance: true, IncludeDependencies: true, IncludeProperties: false,
                IncludeFunctions: false));

        // Assert
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Address");
        result.Should().Contain("-->", "Relationships should still be present");

        // Members should be hidden
        result.Should().NotContain("int Id");
        result.Should().NotContain("string Name");
    }

    [Fact]
    public async Task GetClassDiagram_NoOptionalParams_ShouldShowAllMembers()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        // Call with only the mandatory filePath
        var result = await tools.GetClassDiagramAsync(_tempFile);

        // Assert
        result.Should().Contain("class TestNamespace_Person");
        result.Should().Contain("int Id");
        result.Should().Contain("string Name");
    }

    [Fact]
    public async Task GetClassDiagram_ComplexClass_ShouldSignificantlyReduceCharacterCountWhenMembersHidden()
    {
        // Arrange
        var tools = CreateTools();
        var complexFile = Path.Combine(_temp.DirectoryPath, "Complex.cs");
        const string code = """
                            namespace Test;
                            public class Complex
                            {
                                public int P1 { get; set; }
                                public int P2 { get; set; }
                                public int P3 { get; set; }
                                public int P4 { get; set; }
                                public int P5 { get; set; }
                                public int P6 { get; set; }
                                public int P7 { get; set; }
                                public int P8 { get; set; }
                                public int P9 { get; set; }
                                public int P10 { get; set; }
                                public void M1() {}
                                public void M2() {}
                                public void M3() {}
                                public void M4() {}
                                public void M5() {}
                            }
                            """;
        await File.WriteAllTextAsync(complexFile, code);

        // Act
        var resultWithMembers =
            await tools.GetClassDiagramAsync(complexFile,
                new AnalysisOptions(IncludeProperties: true, IncludeFunctions: true));
        var resultWithoutMembers =
            await tools.GetClassDiagramAsync(complexFile,
                new AnalysisOptions(IncludeProperties: false, IncludeFunctions: false));

        // Assert
        var withCount = resultWithMembers.Length;
        var withoutCount = resultWithoutMembers.Length;

        // With 15 members, reduction should be > 50%
        var reduction = (double)(withCount - withoutCount) / withCount;
        reduction.Should().BeGreaterThanOrEqualTo(0.5,
            $"Hiding members should reduce diagram size significantly. Reduced by {reduction:P}");
    }

    [Fact]
    public async Task GetClassDiagram_Directory_ShouldReturnCombinedDiagram()
    {
        // Arrange
        var tools = CreateTools();
        var modelsDir = GetProjectPath("samples/classdiagram/simple-hierarchy/Models");

        // Act
        // Use IncludeInheritance: true to see inheritance relationships (like Admin --|> User)
        var result = await tools.GetClassDiagramAsync(modelsDir, new AnalysisOptions(IncludeInheritance: true));

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("class SimpleHierarchy_Models_User");
        result.Should().Contain("class SimpleHierarchy_Models_Admin");
        result.Should().Contain("class SimpleHierarchy_Models_Address");
        result.Should().Contain("<|--", "Inheritance relationships should be present");
    }

    private static string GetProjectPath(string relativePath)
    {
        var parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var pathParts = new[]
            {
                Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."
            }
            .Concat(parts)
            .ToArray();
        var path = Path.Combine(pathParts);
        return Path.GetFullPath(path);
    }
}
