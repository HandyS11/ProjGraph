using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

[Trait("Category", "Core")]
public class SlnxParserTests
{
    private readonly SlnxParser _parser = new(new PhysicalFileSystem());

    [Fact]
    public void GetProjectPaths_ShouldExtractPathsFromSlnx()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="src/ProjA/ProjA.csproj" />
                                 <Project Path="tests/ProjA.Tests/ProjA.Tests.csproj" />
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("test.slnx", content);

        // Act
        var paths = _parser.GetProjectPaths(tempSlnx).ToList();

        // Assert
        paths.Should().HaveCount(2);
        paths.Any(p => p.EndsWith("ProjA.csproj", StringComparison.Ordinal)).Should().BeTrue();
        paths.Any(p => p.EndsWith("ProjA.Tests.csproj", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnEmptyWhenFileDoesNotExist()
    {
        // Arrange
        using var temp = new TestDirectory();
        var nonExistentPath = temp.GetTempFilePath(".slnx");

        // Act
        var paths = _parser.GetProjectPaths(nonExistentPath).ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleEmptySlnx()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("empty.slnx", content);

        // Act
        var paths = _parser.GetProjectPaths(tempSlnx).ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void GetProjectPaths_ShouldIgnoreProjectsWithoutPathAttribute()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="src/ProjA/ProjA.csproj" />
                                 <Project Name="InvalidProject" />
                                 <Project Path="tests/ProjB/ProjB.csproj" />
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("ignore_invalid.slnx", content);

        // Act
        var paths = _parser.GetProjectPaths(tempSlnx).ToList();

        // Assert
        paths.Should().HaveCount(2);
        paths.Any(p => p.EndsWith("ProjA.csproj", StringComparison.Ordinal)).Should().BeTrue();
        paths.Any(p => p.EndsWith("ProjB.csproj", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void GetProjectPaths_ShouldResolveRelativePaths()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="../OtherDir/ProjA.csproj" />
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("resolve.slnx", content);

        // Act
        var paths = _parser.GetProjectPaths(tempSlnx).ToList();

        // Assert
        paths.Should().HaveCount(1);
        paths[0].Should().EndWith("ProjA.csproj");
        Path.IsPathRooted(paths[0]).Should().BeTrue();
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleInvalidXml()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="src/ProjA/ProjA.csproj"
                               <!-- Missing closing tags
                               """;

        var tempSlnx = temp.CreateFile("invalid.slnx", content);

        // Act & Assert
        var act = () => _parser.GetProjectPaths(tempSlnx).ToList();
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleWindowsAndUnixPaths()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="src\ProjA\ProjA.csproj" />
                                 <Project Path="src/ProjB/ProjB.csproj" />
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("paths.slnx", content);

        // Act
        var paths = _parser.GetProjectPaths(tempSlnx).ToList();

        // Assert
        paths.Should().HaveCount(2);
        paths.All(Path.IsPathRooted).Should().BeTrue();
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleMultipleDifferentProjects()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="A/A.csproj" />
                                 <Project Path="B/B.csproj" />
                                 <Project Path="C/C.csproj" />
                                 <Project Path="D/D.csproj" />
                                 <Project Path="E/E.csproj" />
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("multiple.slnx", content);

        // Act
        var paths = _parser.GetProjectPaths(tempSlnx).ToList();

        // Assert
        paths.Should().HaveCount(5);
        paths.Should().OnlyHaveUniqueItems();
    }
}
