using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpProjectGraphTests
{
    [Fact]
    public async Task GetProjectGraph_SimpleDependencies_Slnx_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();
        var slnxPath = GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var result = await tools.GetProjectGraphAsync(slnxPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("A");
        result.Should().Contain("B");
        result.Should().Contain("C");
        result.Should().Contain("D");
        result.Should().Contain("-->");
        result.Trim().Should().EndWith("```");
    }

    [Fact]
    public async Task GetProjectGraph_SimpleDependencies_SingleProject_ShouldDiscoverAllDependencies()
    {
        // Arrange
        var tools = CreateTools();
        var projPath = GetSamplePath(@"visualize\simple-dependencies\A\A.csproj");

        // Act
        var result = await tools.GetProjectGraphAsync(projPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("A");
        result.Should().Contain("B");
        result.Should().Contain("-->");
        result.Trim().Should().EndWith("```");
    }

    [Fact]
    public async Task GetProjectGraph_ProjGraphSolution_Slnx_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();
        var slnxPath = GetRootPath("ProjGraph.slnx");

        // Act
        var result = await tools.GetProjectGraphAsync(slnxPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("ProjGraph_Cli");
        result.Should().Contain("ProjGraph_Core");
        result.Should().Contain("ProjGraph_Lib");
        result.Should().Contain("ProjGraph_Mcp");
        result.Trim().Should().EndWith("```");
    }

    [Fact]
    public async Task GetProjectGraph_SimpleDependencies_ShouldShowCorrectRelationships()
    {
        // Arrange
        var tools = CreateTools();
        var slnxPath = GetSamplePath("visualize/simple-dependencies/simple-dependencies.slnx");

        // Act
        var result = await tools.GetProjectGraphAsync(slnxPath);

        // Assert
        result.Should().Contain("A --> B");
        result.Should().Contain("B --> C");
        result.Should().Contain("B --> D");
    }

    [Fact]
    public async Task GetProjectGraph_NonExistentFile_ShouldThrow()
    {
        // Arrange
        var tools = CreateTools();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "this", "path", "does", "not", "exist.slnx");

        // Act
        var act = async () => await tools.GetProjectGraphAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetProjectGraph_InvalidFile_ShouldThrow()
    {
        // Arrange
        var tools = CreateTools();
        var invalidPath = GetRootPath("README.md"); // Not a solution/project file

        // Act
        var act = async () => await tools.GetProjectGraphAsync(invalidPath);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    private static string GetSamplePath(string relativePath)
    {
        return TestPathHelper.GetSamplePath(relativePath);
    }

    private static string GetRootPath(string relativePath)
    {
        return TestPathHelper.GetRootPath(relativePath);
    }

    private static ProjGraphTools CreateTools()
    {
        return McpTestHelper.CreateTools();
    }
}
