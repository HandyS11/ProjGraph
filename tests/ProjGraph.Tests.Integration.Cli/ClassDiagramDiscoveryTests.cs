using ProjGraph.Tests.Integration.Cli.Helpers;
using Projgraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public class ClassDiagramDiscoveryTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempRoot;

    public ClassDiagramDiscoveryTests()
    {
        _tempRoot = _temp.DirectoryPath;
    }

    public void Dispose()
    {
        _temp.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ClassDiagramCommand_WithDiscovery_IncludesRelatedTypes()
    {
        // Arrange
        var modelsDir = Path.Combine(_tempRoot, "Models");
        Directory.CreateDirectory(modelsDir);

        var serviceFile = Path.Combine(_tempRoot, "UserService.cs");
        var userFile = Path.Combine(modelsDir, "User.cs");

        // Add a dummy project file to help discovery find a root
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "Test.csproj"), "<Project />");

        await File.WriteAllTextAsync(serviceFile, """
                                                  namespace TestNamespace;

                                                  public class UserService 
                                                  {
                                                      public User GetUser() => new User();
                                                  }
                                                  """);
        await File.WriteAllTextAsync(userFile, """
                                               namespace TestNamespace;

                                               public class User 
                                               {
                                                   public string Name { get; set; }
                                               }
                                               """);

        // Verify files were created
        File.Exists(serviceFile).Should().BeTrue("Service file should exist");
        File.Exists(userFile).Should().BeTrue("User file should exist");

        // Act
        var app = CliTestHelpers.CreateApp();
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var args = new[] { "classdiagram", serviceFile, "--dependencies", "--depth", "1" };
            resultCode = app.Run(args);
        });

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Service file: {serviceFile}, Output: {capturedOutput}");
        capturedOutput.Should().NotBeNullOrEmpty("Output should contain diagram content");
        capturedOutput.Should().Contain("classDiagram", "Output should contain Mermaid class diagram header");
        capturedOutput.Should().Contain("UserService",
            $"Output should contain UserService class. Full output: {capturedOutput}");
        capturedOutput.Should().Contain("User", $"Output should contain User class. Full output: {capturedOutput}");
        capturedOutput.Should().MatchRegex(@"(-->|\.\.>)",
            "Output should contain relationship arrow (association or dependency)");
    }
}