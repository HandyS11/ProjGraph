using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.ProjectGraph.Rendering;

namespace ProjGraph.Tests.Unit.ProjectGraph;

[Trait("Category", "ProjectGraph")]
public class MermaidGraphRendererTests
{
    private readonly MermaidGraphRenderer _renderer = new();

    [Fact]
    public void Render_ShouldGenerateValidMermaidSyntax()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "ProjectA", "ProjectA.csproj", "ProjectA.csproj", "net10.0", ProjectType.Library),
            new(guidB, "ProjectB", "ProjectB.csproj", "ProjectB.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, dependencies);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("ProjectA");
        result.Should().Contain("ProjectB");
        result.Should().Contain("-->");
    }

    [Theory]
    [InlineData(ProjectType.Executable, "(Exe)")]
    [InlineData(ProjectType.Test, "(Test)")]
    public void Render_ShouldLabelNonLibraryProjects(ProjectType projectType, string expectedLabel)
    {
        // Arrange
        var guidA = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "MyProject", "MyProject.csproj", "MyProject.csproj", "net10.0", projectType)
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain(expectedLabel);
    }

    [Fact]
    public void Render_ShouldNotLabelLibraryProjects()
    {
        // Arrange
        var guidA = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "Library", "Library.csproj", "Library.csproj", "net10.0", ProjectType.Library)
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("Library[\"Library\"]");
        result.Should().NotContain("(Exe)");
        result.Should().NotContain("(Test)");
    }

    [Fact]
    public void Render_ShouldUseRoundedNodesForPackages()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var projects = new List<Project>
        {
            new(guid, "Newtonsoft.Json", "13.0.1", "13.0.1", "net10.0", ProjectType.Package)
        };

        var graph = new SolutionGraph("Test", "test.csproj", projects, []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("Newtonsoft_Json{{");
        result.Should().Contain("Newtonsoft.Json 13.0.1");
        result.Should().Contain("classDef pkg");
        result.Should().Contain("class Newtonsoft_Json pkg");
    }

    [Fact]
    public void Render_ShouldShowTitleFromSolutionName()
    {
        // Arrange
        var graph = new SolutionGraph("MySolution", "MySolution.sln", [], []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("---");
        result.Should().Contain("title: MySolution");
    }

    [Fact]
    public void Render_ShouldNotShowTitle_WhenShowTitleIsFalse()
    {
        // Arrange
        var graph = new SolutionGraph("MySolution", "MySolution.sln", [], []);

        // Act
        var result = _renderer.Render(graph, new DiagramOptions(false));

        // Assert
        result.Should().NotContain("---");
        result.Should().NotContain("title: MySolution");
    }

    [Fact]
    public void Render_ShouldHandleEmptyGraph()
    {
        // Arrange
        var graph = new SolutionGraph("EmptySolution", "EmptySolution.sln", [], []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("```");
    }

    [Theory]
    [InlineData("My.Project.Name")]
    [InlineData("My-Project-Name")]
    public void Render_ShouldSanitizeProjectNamesWithSpecialCharacters(string projectName)
    {
        // Arrange
        var guidA = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, projectName, $"{projectName}.csproj", $"{projectName}.csproj", "net10.0",
                ProjectType.Library)
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("My_Project_Name");
    }

    [Fact]
    public void Render_ShouldHandleMultipleDependencies()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();
        var guidC = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library),
            new(guidC, "C", "C.csproj", "C.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference),
            new(guidA, guidC, DependencyType.ProjectReference),
            new(guidB, guidC, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, dependencies);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("A --> B");
        result.Should().Contain("A --> C");
        result.Should().Contain("B --> C");
    }

    [Fact]
    public void Render_ShouldHandleCircularDependencies()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference),
            new(guidB, guidA, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, dependencies);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("A --> B");
        result.Should().Contain("B --> A");
    }

    [Fact]
    public void Render_ShouldIgnoreDependenciesWithMissingProjects()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();
        var guidMissing = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference),
            new(guidA, guidMissing, DependencyType.ProjectReference) // Missing target
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, dependencies);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.Should().Contain("A --> B");
        result.Split('\n').Count(line => line.Contains("-->", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void Render_ShouldProduceValidMermaidClosingTag()
    {
        // Arrange
        var graph = new SolutionGraph("Test", "Test.sln", [], []);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        result.TrimEnd().Should().EndWith("```");
    }

    [Fact]
    public void Render_ShouldOrderProjectsAndDependenciesByName()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();
        var guidC = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidC, "ProjectC", "C.csproj", "C.csproj", "net10.0", ProjectType.Library),
            new(guidA, "ProjectA", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "ProjectB", "B.csproj", "B.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency>
        {
            new(guidB, guidC, DependencyType.ProjectReference), // B -> C
            new(guidA, guidB, DependencyType.ProjectReference), // A -> B
            new(guidA, guidC, DependencyType.ProjectReference) // A -> C
        };

        var graph = new SolutionGraph("TestSolution", "TestSolution.sln", projects, dependencies);

        // Act
        var result = _renderer.Render(graph);

        // Assert
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Projects should be ordered A, B, C
        var projectLines = lines.Where(l => l.Contains('[', StringComparison.Ordinal)).ToList();
        projectLines[0].Should().Contain("ProjectA");
        projectLines[1].Should().Contain("ProjectB");
        projectLines[2].Should().Contain("ProjectC");

        // Dependencies should be ordered A->B, A->C, B->C
        var dependencyLines = lines.Where(l => l.Contains("-->", StringComparison.Ordinal)).ToList();
        dependencyLines[0].Should().Contain("ProjectA --> ProjectB");
        dependencyLines[1].Should().Contain("ProjectA --> ProjectC");
        dependencyLines[2].Should().Contain("ProjectB --> ProjectC");
    }
}
