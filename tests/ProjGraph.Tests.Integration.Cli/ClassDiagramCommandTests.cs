using ProjGraph.Tests.Integration.Cli.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using Spectre.Console.Cli;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public sealed class ClassDiagramCommandTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void ClassDiagramCommand_SimpleHierarchy_User_ShouldGenerateClassDiagram()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");

        File.Exists(userPath).Should().BeTrue($"Sample file should exist at: {userPath}");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", userPath]));

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Output: {capturedOutput}");
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("User");
        capturedOutput.Should().Contain("Username");
        capturedOutput.Should().Contain("Email");
    }

    [Fact]
    public void ClassDiagramCommand_SimpleHierarchy_User_WithInheritance_ShouldIncludeBaseClass()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", userPath, "--inheritance"]));

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Output: {capturedOutput}");
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("User");
        capturedOutput.Should().Contain("BaseEntity");
        capturedOutput.Should().Contain("<|--", "Should contain inheritance arrow");
    }

    [Fact]
    public void ClassDiagramCommand_SimpleHierarchy_User_WithDependencies_ShouldIncludeAddress()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", userPath, "--dependencies"]));

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Output: {capturedOutput}");
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("User");
        capturedOutput.Should().Contain("Address");
        capturedOutput.Should().Contain("-->", "Should contain association arrow");
    }

    [Fact]
    public void ClassDiagramCommand_SimpleHierarchy_Admin_WithInheritance_ShouldShowFullChain()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var adminPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\Admin.cs");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", adminPath, "--inheritance", "--depth", "2"]));

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Output: {capturedOutput}");
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("Admin");
        capturedOutput.Should().Contain("User");
        capturedOutput.Should().Contain("Permissions");
    }

    [Fact]
    public void ClassDiagramCommand_SimpleHierarchy_Address_ShouldShowProperties()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var addressPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\Address.cs");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", addressPath]));

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Output: {capturedOutput}");
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("Address");
        capturedOutput.Should().Contain("Street");
        capturedOutput.Should().Contain("City");
        capturedOutput.Should().Contain("ZipCode");
        capturedOutput.Should().Contain("Country");
    }

    [Fact]
    public void ClassDiagramCommand_WithShowTitleFalse_ShouldOmitTitle()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", userPath, "--show-title", "false"]));

        // Assert
        resultCode.Should().Be(0);
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().NotContain("title:");
    }

    [Fact]
    public void ClassDiagramCommand_NonExistentFile_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        const string nonExistentPath = "NonExistentFile.cs";

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["classdiagram", nonExistentPath]));

        exception.Message.Should().Contain("Path not found");
    }

    [Fact]
    public void ClassDiagramCommand_NonCsFile_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["classdiagram", slnxPath]));

        exception.Message.Should().Contain(".cs");
    }

    [Fact]
    public void ClassDiagramCommand_ComplexHierarchy_UserService_WithAllFlags_ShouldShowRelationships()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var servicePath = CliTestHelpers.GetSamplePath(
            @"classdiagram\complex-hierarchy\Services\Implementations\UserService.cs");

        File.Exists(servicePath).Should().BeTrue($"Sample file should exist at: {servicePath}");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", servicePath, "--inheritance", "--dependencies", "--depth", "2"]));

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Output: {capturedOutput}");
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("UserService");
    }

    [Fact]
    public async Task ClassDiagramCommand_WithDiscovery_IncludesRelatedTypes()
    {
        // Arrange
        var tempRoot = _temp.DirectoryPath;
        var modelsDir = Path.Combine(tempRoot, "Models");
        Directory.CreateDirectory(modelsDir);

        var serviceFile = Path.Combine(tempRoot, "UserService.cs");
        var userFile = Path.Combine(modelsDir, "User.cs");

        // Add a dummy project file to help discovery find a root
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "Test.csproj"), "<Project />");

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
            var args = new[]
            {
                "classdiagram", serviceFile, "--dependencies", "--depth", "1"
            };
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

    [Fact]
    public void ClassDiagramCommand_HideProperties_ShouldNotShowProperties()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            app.Run(["classdiagram", userPath, "--properties", "false"]));

        // Assert
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("[\"User\"]");
        capturedOutput.Should().NotContain("+string Username");
        capturedOutput.Should().NotContain("+string Email");
    }

    [Fact]
    public void ClassDiagramCommand_HideFunctions_ShouldNotShowMethods()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            app.Run(["classdiagram", userPath, "--functions", "false"]));

        // Assert
        capturedOutput.Should().Contain("classDiagram");
        capturedOutput.Should().Contain("[\"User\"]");
        // User in simple-hierarchy has only properties, so we check they are still there
        capturedOutput.Should().Contain("+string Username");
    }

    [Fact]
    public async Task ClassDiagramCommand_HideFunctions_WithMethods_ShouldExcludeMethods()
    {
        // Arrange
        var tempDir = _temp.DirectoryPath;
        var filePath = Path.Combine(tempDir, "Svc.cs");
        await File.WriteAllTextAsync(filePath, "namespace Test; public class Svc { public void DoWork() {} }");

        var app = CliTestHelpers.CreateApp();

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            app.Run(["classdiagram", filePath, "--functions", "false"]));

        // Assert
        capturedOutput.Should().Contain("[\"Svc\"]");
        capturedOutput.Should().NotContain("DoWork()");
    }

    [Fact]
    public async Task ClassDiagramCommand_FileOutput_ShouldSaveToDisk()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var userPath = CliTestHelpers.GetSamplePath(@"classdiagram\simple-hierarchy\Models\User.cs");
        var outputPath = Path.Combine(Path.GetTempPath(), "classdiagram_" + Guid.NewGuid() + ".md");

        try
        {
            // Act
            var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            {
                var result = app.Run(["classdiagram", userPath, "--output", outputPath]);
                result.Should().Be(0);
            });

            // Assert
            capturedOutput.Should().Contain($"Saved to {outputPath}");
            capturedOutput.Should().NotContain("classDiagram");

            File.Exists(outputPath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(outputPath);
            fileContent.Should().Contain("```mermaid");
            fileContent.Should().Contain("classDiagram");
            fileContent.Should().Contain("User");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ClassDiagramCommand_Directory_SimpleHierarchy_ShouldGenerateCombinedClassDiagram()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var modelsDir = CliTestHelpers.GetSamplePath("classdiagram/simple-hierarchy/Models");

        Directory.Exists(modelsDir).Should().BeTrue($"Sample directory should exist at: {modelsDir}");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", modelsDir, "-i", "-d"]));

        // Assert
        resultCode.Should().Be(0);
        capturedOutput.Should().Contain("class SimpleHierarchy_Models_User");
        capturedOutput.Should().Contain("class SimpleHierarchy_Models_Admin");
        capturedOutput.Should().Contain("class SimpleHierarchy_Models_Address");
        capturedOutput.Should().Contain("SimpleHierarchy_Models_User <|-- SimpleHierarchy_Models_Admin");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ClassDiagramCommand_Directory_Recursive_ShouldIncludeNestedTypes()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        // The simple-hierarchy root has Models/ and other folders
        var rootDir = CliTestHelpers.GetSamplePath("classdiagram/simple-hierarchy");

        Directory.Exists(rootDir).Should().BeTrue($"Sample directory should exist at: {rootDir}");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            resultCode = app.Run(["classdiagram", rootDir, "-i", "-d"]));

        // Assert
        resultCode.Should().Be(0);
        // Models/User.cs
        capturedOutput.Should().Contain("class SimpleHierarchy_Models_User");
        // Mappers/UserMapper.cs
        capturedOutput.Should().Contain("class SimpleHierarchy_Mappers_UserMapper");
    }
}
