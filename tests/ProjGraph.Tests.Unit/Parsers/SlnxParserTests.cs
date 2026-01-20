using FluentAssertions;
using ProjGraph.Lib.Parsers;

namespace ProjGraph.Tests.Unit.Parsers;

public class SlnxParserTests
{
    [Fact]
    public void GetProjectPaths_ShouldExtractPathsFromSlnx()
    {
        // Arrange
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="src/ProjA/ProjA.csproj" />
                                 <Project Path="tests/ProjA.Tests/ProjA.Tests.csproj" />
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = SlnxParser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().HaveCount(2);
            paths.Any(p => p.EndsWith("ProjA.csproj")).Should().BeTrue();
            paths.Any(p => p.EndsWith("ProjA.Tests.csproj")).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnEmptyWhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        // Act
        var paths = SlnxParser.GetProjectPaths(nonExistentPath).ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleEmptySlnx()
    {
        // Arrange
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = SlnxParser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldIgnoreProjectsWithoutPathAttribute()
    {
        // Arrange
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="src/ProjA/ProjA.csproj" />
                                 <Project Name="InvalidProject" />
                                 <Project Path="tests/ProjB/ProjB.csproj" />
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = SlnxParser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().HaveCount(2);
            paths.Any(p => p.EndsWith("ProjA.csproj")).Should().BeTrue();
            paths.Any(p => p.EndsWith("ProjB.csproj")).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldResolveRelativePaths()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempSlnx = Path.Combine(tempDir, "test.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="../OtherDir/ProjA.csproj" />
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = SlnxParser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().HaveCount(1);
            paths[0].Should().EndWith("ProjA.csproj");
            Path.IsPathRooted(paths[0]).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleInvalidXml()
    {
        // Arrange
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="src/ProjA/ProjA.csproj"
                               <!-- Missing closing tags
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act & Assert
            var act = () => SlnxParser.GetProjectPaths(tempSlnx).ToList();
            act.Should().Throw<Exception>();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleWindowsAndUnixPaths()
    {
        // Arrange
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="src\ProjA\ProjA.csproj" />
                                 <Project Path="src/ProjB/ProjB.csproj" />
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = SlnxParser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().HaveCount(2);
            paths.All(p => Path.IsPathRooted(p)).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldHandleMultipleDifferentProjects()
    {
        // Arrange
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="A/A.csproj" />
                                 <Project Path="B/B.csproj" />
                                 <Project Path="C/C.csproj" />
                                 <Project Path="D/D.csproj" />
                                 <Project Path="E/E.csproj" />
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = SlnxParser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().HaveCount(5);
            paths.Should().OnlyHaveUniqueItems();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }
}