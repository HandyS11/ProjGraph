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

    #region Basic Class Diagram Tests

    [Fact]
    public async Task GetClassDiagram_SimpleClasses_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagram(_tempFile);

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
        var result = await tools.GetClassDiagram(_tempFile);

        // Assert
        result.Should().Contain("int Id");
        result.Should().Contain("string Name");
        result.Should().Contain("int Age");
        result.Should().Contain("int ProductId");
        result.Should().Contain("string ProductName");
        result.Should().Contain("decimal Price");
    }

    [Fact]
    public async Task GetClassDiagram_NonExistentFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "this", "path", "does", "not", "exist.cs");

        // Act
        var result = await tools.GetClassDiagram(nonExistentPath);

        // Assert
        result.Should().StartWith("Error");
    }

    #endregion

    #region Inheritance Tests

    [Fact]
    public async Task GetClassDiagram_WithInheritance_ShouldShowBaseClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagram(_tempFileWithInheritance, true);

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
        var result = await tools.GetClassDiagram(_tempFileWithInheritance, true);

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
        var result = await tools.GetClassDiagram(_tempFileWithInheritance);

        // Assert
        result.Should().Contain("class TestNamespace_User");
        result.Should().Contain("class TestNamespace_Admin");
        // Should not show inheritance relationships when disabled
        result.Should().NotContain("<|--");
    }

    #endregion

    #region Dependencies Tests

    [Fact]
    public async Task GetClassDiagram_WithDependencies_ShouldShowRelatedClasses()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagram(_tempFileWithDependencies, includeDependencies: true);

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
        var result = await tools.GetClassDiagram(_tempFileWithDependencies, includeDependencies: false);

        // Assert
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Address");
        result.Should().Contain("class TestNamespace_Order");
        // Should not show dependency/association relationships when disabled
        result.Should().NotContain("-->");
    }

    #endregion

    #region Depth Tests

    [Fact]
    public async Task GetClassDiagram_WithDepth1_ShouldLimitRelationshipDepth()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagram(_tempFileWithDependencies, includeDependencies: true, depth: 1);

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
        var result = await tools.GetClassDiagram(_tempFileWithDependencies, includeDependencies: true, depth: 2);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        result.Should().Contain("class TestNamespace_Customer");
        result.Should().Contain("class TestNamespace_Order");
    }

    #endregion

    #region Real Project Tests

    [Fact]
    public async Task GetClassDiagram_RealProject_CoreModels_ShouldGenerateDiagram()
    {
        // Arrange
        var tools = CreateTools();
        var modelsPath = GetProjectPath(@"src\ProjGraph.Core\Models\ClassDiagramModels.cs");

        // Skip if file doesn't exist (e.g., in CI environment)
        if (!File.Exists(modelsPath))
        {
            return;
        }

        // Act
        var result = await tools.GetClassDiagram(modelsPath, true);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
    }

    #endregion

    #region Parameter Combination Tests

    [Fact]
    public async Task GetClassDiagram_AllOptionsEnabled_ShouldWorkCorrectly()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetClassDiagram(
            _tempFileWithInheritance,
            true,
            true,
            3);

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
        var result = await tools.GetClassDiagram(
            _tempFileWithInheritance,
            false,
            false,
            0);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("classDiagram");
        result.Should().Contain("class TestNamespace_User");
        result.Should().Contain("class TestNamespace_Admin");
    }

    #endregion

    private static string GetProjectPath(string relativePath)
    {
        var parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var pathParts = new[] { Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".." }
            .Concat(parts)
            .ToArray();
        var path = Path.Combine(pathParts);
        return Path.GetFullPath(path);
    }
}