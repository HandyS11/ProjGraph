using ProjGraph.Cli;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace ProjGraph.Tests.Integration.Cli;

/// <summary>
/// Tests for <c>projgraph --version</c>. They run the real configuration from
/// <see cref="Program.Configure"/> against a <see cref="TestConsole"/> rather than calling
/// <see cref="Program.Main"/>, because only one test in the assembly may reach Spectre's cached default
/// console (see <see cref="StrictParsingTests"/>).
/// </summary>
[Collection("CLI Tests")]
public sealed partial class VersionOptionTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void VersionOption_PrintsThePackageVersionAndSucceeds(string option)
    {
        var console = new TestConsole();
        var app = new CommandApp();
        app.Configure(config =>
        {
            Program.Configure(config);
            config.Settings.Console = console;
        });

        var exitCode = app.Run([option]);

        exitCode.Should().Be(0, $"output:\n{console.Output}");
        var printed = console.Output.Trim();
        printed.Should().MatchRegex(PackageVersion(),
            "the version must be a plain package version, without the +<commit> build metadata");
        // The assembly version is the numeric part of the package version, so this ties the printed
        // value to the CLI assembly and not to the test host's entry assembly.
        var assemblyVersion = typeof(Program).Assembly.GetName().Version!;
        printed.Should().StartWith(assemblyVersion.ToString(3));
    }

    [Fact]
    public void GetPackageVersion_WithoutInformationalVersionAttribute_FallsBackToTheAssemblyVersion()
    {
        // Configure runs for every command, so a build without the attribute must not break them all.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("NoInformationalVersion") { Version = new Version(2, 3, 4, 5) },
            AssemblyBuilderAccess.Run);

        Program.GetPackageVersion(assembly).Should().Be("2.3.4");
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex PackageVersion();
}
