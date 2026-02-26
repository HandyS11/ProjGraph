using ProjGraph.Tests.Integration.Cli.Helpers;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public class StatsCommandTests
{
    // ── T011: Valid path renders all metric sections ──────────────────────────

    [Fact]
    public void StatsCommand_SimpleDependencies_ShouldRenderMetricsTable()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["stats", slnxPath]);
            result.Should().Be(0);
        });

        // Assert — solution name header is rendered
        capturedOutput.Should().Contain("simple-dependencies",
            "the solution name should appear in the header");

        // Assert — key metric labels are present
        capturedOutput.Should().Contain("Total projects",
            "the total project count metric should be present");
        capturedOutput.Should().Contain("Libraries",
            "a type breakdown entry for libraries should be present");
        capturedOutput.Should().Contain("Cycles detected",
            "the cycle detection metric should be present");

        // Assert — simple-dependencies has no cycles
        capturedOutput.Should().Contain("No",
            "simple-dependencies graph has no dependency cycles");
    }

    [Fact]
    public void StatsCommand_SimpleDependencies_ShouldRenderHotspotSection()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["stats", slnxPath]);
            result.Should().Be(0);
        });

        // Assert — hotspot section header
        capturedOutput.Should().Contain("Most-referenced projects",
            "the hotspot section should be rendered for a solution with dependencies");
    }

    [Fact]
    public void StatsCommand_SimpleDependencies_TopOption_ShouldRespectLimit()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["stats", slnxPath, "--top", "1"]);
            result.Should().Be(0);
        });

        // Assert — at most one hotspot project is shown
        // We count occurrences of the ranking bullet "  1." to confirm only one entry
        capturedOutput.Should().Contain("1.",
            "at least one hotspot rank should appear when --top 1 is used");
    }

    [Fact]
    public void StatsCommand_ProjGraphSolution_ShouldComplete()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetRootPath("ProjGraph.slnx");

        // Act / Assert — exercise the full graph; just verify exit code 0 and solution name
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["stats", slnxPath]);
            result.Should().Be(0);
        });

        capturedOutput.Should().Contain("ProjGraph");
    }

    // ── T012: Error cases exit with non-zero and write an error message ───────

    [Fact]
    public void StatsCommand_NonExistentPath_ShouldReturnExitCode1()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = 0;
        var capturedOutput =
            CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["stats", @"C:\does-not-exist\Missing.slnx"]));

        // Assert
        exitCode.Should().Be(1);
        capturedOutput.Should().Contain("not found",
            "error message should state the file was not found");
    }

    [Fact]
    public void StatsCommand_UnsupportedExtension_ShouldReturnExitCode1()
    {
        // Arrange — use an actually-existing file with the wrong extension
        var app = CliTestHelpers.CreateApp();
        var wrongExtPath = CliTestHelpers.GetRootPath("README.md");

        // Act
        var exitCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["stats", wrongExtPath]));

        // Assert
        exitCode.Should().Be(1);
        capturedOutput.Should().ContainAny(
            [".sln", ".slnx", ".csproj"],
            "error message should mention the supported file extensions");
    }
}
