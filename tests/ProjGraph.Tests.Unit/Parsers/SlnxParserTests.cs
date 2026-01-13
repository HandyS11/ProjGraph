using FluentAssertions;
using ProjGraph.Lib.Parsers;

namespace ProjGraph.Tests.Unit.Parsers;

public class SlnxParserTests
{
    [Fact]
    public void GetProjectPaths_ShouldExtractPathsFromSlnx()
    {
        // Arrange
        var parser = new SlnxParser();
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        var content = """
            <Solution>
              <Project Path="src/ProjA/ProjA.csproj" />
              <Project Path="tests/ProjA.Tests/ProjA.Tests.csproj" />
            </Solution>
            """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var paths = parser.GetProjectPaths(tempSlnx).ToList();

            // Assert
            paths.Should().HaveCount(2);
            paths.Any(p => p.EndsWith("ProjA.csproj")).Should().BeTrue();
            paths.Any(p => p.EndsWith("ProjA.Tests.csproj")).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempSlnx)) File.Delete(tempSlnx);
        }
    }
}
