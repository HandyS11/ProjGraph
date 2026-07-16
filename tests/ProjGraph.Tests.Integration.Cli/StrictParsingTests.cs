using ProjGraph.Cli;
using ProjGraph.Tests.Integration.Cli.Helpers;
using Spectre.Console.Cli;

namespace ProjGraph.Tests.Integration.Cli;

/// <summary>
/// Regression tests for the silently-ignored-option bug (issue #162): without
/// <c>StrictParsing</c>, Spectre.Console.Cli dropped unrecognized long options on every command,
/// so a typo like <c>--owned-mod classic</c> exited 0 and rendered in the default mode with no
/// signal to the user.
/// </summary>
[Collection("CLI Tests")]
public sealed class StrictParsingTests
{
    /// <summary>
    /// Runs the REAL app configuration via <see cref="Program.Main"/>, not the test harness's own
    /// <c>CommandApp</c>, so a regression in Program.cs itself is caught. This must remain the ONLY
    /// test in the assembly that reaches Spectre's default (non-propagating) error rendering:
    /// Spectre.Console.Cli caches <c>AnsiConsole.Console</c> in a process-lifetime <c>Lazy</c> on
    /// first render (Internal/Extensions/AnsiConsoleExtensions.cs), so a second such test would
    /// write to this test's already-disposed capture writer and crash with ObjectDisposedException.
    /// </summary>
    [Fact]
    public void UnknownLongOption_FailsWithNonZeroExitCodeAndNamesTheOption()
    {
        var exitCode = 0;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            // The typo'd form of --owned-mode that motivated the issue. Parsing fails before the
            // command executes, so the nonexistent input path is never touched.
            exitCode = Program.Main(["erd", "DoesNotExist.cs", "--owned-mod", "classic"]);
        });

        exitCode.Should().NotBe(0, "an unknown option must fail the invocation, not be silently dropped");
        output.Should().Contain("owned-mod", "the error must tell the user which option was not recognized");
    }

    [Fact]
    public void UnknownLongOption_OnAnotherCommand_ThrowsParseExceptionUnderStrictParsing()
    {
        // The pre-fix behaviour affected every command, not just erd. The harness app propagates
        // exceptions, so strict parsing surfaces as a CommandParseException here.
        var app = CliTestHelpers.CreateApp();

        var act = () => app.Run(["stats", "DoesNotExist.slnx", "--tpo", "5"]);

        act.Should().Throw<CommandParseException>()
            .WithMessage("*tpo*", "the parse error must name the unrecognized option");
    }
}
