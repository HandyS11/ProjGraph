using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Cli.Infrastructure;
using ProjGraph.Tests.Integration.Cli.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using Spectre.Console.Cli;

namespace ProjGraph.Tests.Integration.Cli;

/// <summary>
/// Integration tests for command option validation, error handling and reporting branches that the
/// main per-command suites do not exercise, plus the Spectre.Console DI adapter used to construct
/// the commands.
/// </summary>
[Collection("CLI Tests")]
public sealed class CommandOptionsCoverageTests
{
    private const string GarbageSolutionContent = "this is not a solution file";

    private static string CsprojReferencing(string relativeReference)
    {
        return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{relativeReference}" />
                  </ItemGroup>
                </Project>
                """;
    }

    // ── visualize ─────────────────────────────────────────────────────────────

    [Fact]
    public void VisualizeCommand_NoPath_ShouldFailValidation()
    {
        // Arrange — the path argument is declared optional so Spectre can show help, which means
        // the "required" rule lives in Settings.Validate and must actually fire.
        var app = CliTestHelpers.CreateApp();

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() => app.Run(["visualize"]));

        exception.Message.Should().Contain("Path is required");
    }

    [Fact]
    public void VisualizeCommand_UnsupportedExtension_ShouldReportErrorAndExitOne()
    {
        // Arrange — an existing file with an extension the command does not handle.
        var app = CliTestHelpers.CreateApp();
        var readmePath = CliTestHelpers.GetRootPath("README.md");

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["visualize", readmePath]));

        // Assert
        exitCode.Should().Be(1);
        output.Should().Contain("File must be a .sln, .slnx, or .csproj file.");
        output.Should().NotContain("flowchart", "no diagram should be produced for a rejected input");
    }

    [Fact]
    public void VisualizeCommand_ClassicSlnFile_ShouldBeAccepted()
    {
        // Arrange — the legacy .sln format is an accepted extension alongside .slnx and .csproj.
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("A", "A.csproj"), StandaloneCsproj);
        var slnPath = temp.CreateFile("Legacy.sln", LegacySlnContent);
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["visualize", slnPath, "--format", "flat"]));

        // Assert
        exitCode.Should().Be(0);
        output.Should().Contain("A", "the project declared in the .sln should be listed");
    }

    [Fact]
    public void VisualizeCommand_UnparsableSolutionFile_ShouldReportErrorAndExitOne()
    {
        // Arrange — a .slnx that passes the extension check but cannot be parsed, so the failure
        // surfaces from the graph service and must be turned into a user-facing message.
        using var temp = new TestDirectory();
        var slnxPath = temp.CreateFile("Broken.slnx", GarbageSolutionContent);
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["visualize", slnxPath, "--format", "mermaid"]));

        // Assert
        exitCode.Should().Be(1);
        output.Should().Contain("Failed to read or parse .slnx file");
        output.Should().Contain("Broken.slnx", "the error should name the offending file");
    }

    // ── classdiagram ──────────────────────────────────────────────────────────

    [Fact]
    public void ClassDiagramCommand_NoPath_ShouldFailValidation()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() => app.Run(["classdiagram"]));

        exception.Message.Should().Contain("Path is required");
    }

    [Fact]
    public void ClassDiagramCommand_DirectoryWithManyFiles_ShouldWarnAboutDiagramSize()
    {
        // Arrange — the command warns once a directory scan exceeds 50 files, because the resulting
        // diagram is generally unreadable.
        using var temp = new TestDirectory();
        const int fileCount = 51;
        for (var i = 0; i < fileCount; i++)
        {
            temp.CreateFile($"Type{i}.cs", $"namespace Coverage;\n\npublic class Type{i}\n{{\n}}\n");
        }

        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["classdiagram", temp.DirectoryPath, "--show-title", "false"]));

        // Assert — the warning names the actual count, and the diagram is still produced.
        exitCode.Should().Be(0);
        output.Should().Contain($"Scanning {fileCount} files");
        output.Should().Contain("Large diagrams may be hard to read.");
        output.Should().Contain("classDiagram");
    }

    [Fact]
    public void ClassDiagramCommand_SmallDirectory_ShouldNotWarnAboutDiagramSize()
    {
        // Arrange — the counterpart of the test above: a small scan must stay quiet.
        using var temp = new TestDirectory();
        temp.CreateFile("Only.cs", "namespace Coverage;\n\npublic class Only\n{\n}\n");
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["classdiagram", temp.DirectoryPath, "--show-title", "false"]));

        // Assert
        exitCode.Should().Be(0);
        output.Should().NotContain("Large diagrams may be hard to read.");
        output.Should().Contain("classDiagram");
    }

    [Fact]
    public void ClassDiagramCommand_UnwritableOutputPath_ShouldReportErrorAndExitOne()
    {
        // Arrange — an output path nested underneath an existing *file* cannot be created, so the
        // write fails after a successful analysis and must be reported instead of crashing.
        using var temp = new TestDirectory();
        var sourcePath = temp.CreateFile("Only.cs", "namespace Coverage;\n\npublic class Only\n{\n}\n");
        var outputPath = Path.Combine(sourcePath, "nested", "diagram.md");
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["classdiagram", sourcePath, "--output", outputPath]));

        // Assert
        exitCode.Should().Be(1);
        output.Should().Contain("Error:", "the failure must be surfaced as an error message");
        File.Exists(outputPath).Should().BeFalse();
    }

    // ── stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public void StatsCommand_NoPath_ShouldFailValidation()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() => app.Run(["stats"]));

        exception.Message.Should().Contain("Path is required");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void StatsCommand_NonPositiveTop_ShouldFailValidation(string top)
    {
        // Arrange — --top drives a "take N" projection, so anything below 1 is meaningless.
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["stats", slnxPath, "--top", top]));

        exception.Message.Should().Contain("--top must be at least 1");
    }

    [Fact]
    public void StatsCommand_SolutionWithCycles_ShouldReportCyclesAndSuppressDepth()
    {
        // Arrange — two projects referencing each other. Dependency depth is undefined on a cyclic
        // graph, so the depth rows must be replaced by an explicit "N/A" row.
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("A", "A.csproj"), CsprojReferencing(@"..\B\B.csproj"));
        temp.CreateFile(Path.Combine("B", "B.csproj"), CsprojReferencing(@"..\A\A.csproj"));
        var slnxPath = temp.CreateFile("Cyclic.slnx", """
                                                      <Solution>
                                                        <Project Path="A/A.csproj" />
                                                        <Project Path="B/B.csproj" />
                                                      </Solution>
                                                      """);
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["stats", slnxPath]));

        // Assert
        exitCode.Should().Be(0);
        output.Should().Contain("N/A (cycles detected)");
        output.Should().Contain("Cycles detected");
        output.Should().Contain("Yes");
        output.Should().NotContain("Average depth",
            "depth statistics are meaningless once a cycle is present and must not be shown");
    }

    [Fact]
    public void StatsCommand_UnparsableSolutionFile_ShouldReportErrorAndExitOne()
    {
        // Arrange
        using var temp = new TestDirectory();
        var slnxPath = temp.CreateFile("Broken.slnx", GarbageSolutionContent);
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["stats", slnxPath]));

        // Assert
        exitCode.Should().Be(1);
        output.Should().Contain("Failed to read or parse .slnx file");
        output.Should().NotContain("Total projects", "no metrics table should be rendered on failure");
    }

    // ── DI adapter ────────────────────────────────────────────────────────────

    [Fact]
    public void TypeResolver_ResolveNullType_ShouldReturnNull()
    {
        // Spectre.Console.Cli asks the resolver for a null type when a command has no settings
        // dependency to satisfy; that must not throw.
        using var provider = new ServiceCollection().BuildServiceProvider();
        var resolver = new TypeResolver(provider);

        resolver.Resolve(null).Should().BeNull();
    }

    [Fact]
    public void TypeResolver_ResolveRegisteredType_ShouldReturnTheRegisteredInstance()
    {
        var instance = new SampleDependency();
        var services = new ServiceCollection();
        services.AddSingleton(instance);
        using var provider = services.BuildServiceProvider();
        var resolver = new TypeResolver(provider);

        resolver.Resolve(typeof(SampleDependency)).Should().BeSameAs(instance);
    }

    [Fact]
    public void TypeResolver_Dispose_NonDisposableProvider_ShouldNotThrow()
    {
        // The resolver owns whatever provider it is handed; a provider that is not IDisposable must
        // simply be skipped rather than cast-crashing on dispose.
        var resolver = new TypeResolver(new NonDisposableServiceProvider());

        var act = resolver.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public void TypeRegistrar_BuildAfterRegistrations_ShouldResolveEachRegistrationStyle()
    {
        // All three registration styles used by Spectre.Console.Cli must be honoured by the built
        // resolver.
        var services = new ServiceCollection();
        var registrar = new TypeRegistrar(services);
        var lazyInstance = new SampleDependency();

        registrar.Register(typeof(ISampleDependency), typeof(SampleDependency));
        registrar.RegisterInstance(typeof(SampleDependency), new SampleDependency());
        registrar.RegisterLazy(typeof(SampleDependency[]), () => new[] { lazyInstance });

        var resolver = registrar.Build();

        resolver.Resolve(typeof(ISampleDependency)).Should().BeOfType<SampleDependency>();
        resolver.Resolve(typeof(SampleDependency)).Should().NotBeNull();
        resolver.Resolve(typeof(SampleDependency[])).Should().BeEquivalentTo(new[] { lazyInstance });
    }

    private const string StandaloneCsproj = """
                                            <Project Sdk="Microsoft.NET.Sdk">
                                              <PropertyGroup>
                                                <TargetFramework>net10.0</TargetFramework>
                                              </PropertyGroup>
                                            </Project>
                                            """;

    private const string LegacySlnContent =
        """
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "A", "A\A.csproj", "{11111111-1111-1111-1111-111111111111}"
        EndProject
        Global
        EndGlobal
        """;

    private interface ISampleDependency;

    private sealed class SampleDependency : ISampleDependency;

    /// <summary>An <see cref="IServiceProvider"/> that deliberately does not implement <see cref="IDisposable"/>.</summary>
    private sealed class NonDisposableServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
