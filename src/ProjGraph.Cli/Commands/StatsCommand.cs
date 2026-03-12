using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.ProjectGraph.Application;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;

// ReSharper disable ClassNeverInstantiated.Global

namespace ProjGraph.Cli.Commands;

/// <summary>
/// Represents the command for computing and displaying key architectural metrics for a .NET solution or project.
/// </summary>
/// <param name="statsService">The service used to compute solution metrics.</param>
/// <param name="console">The output console for writing error and status messages.</param>
/// <param name="fileSystem">The file system abstraction for path validation.</param>
internal sealed class StatsCommand(
    IStatsService statsService,
    IOutputConsole console,
    IFileSystem fileSystem)
    : AsyncCommand<StatsCommand.Settings>
{
    /// <summary>
    /// Represents the settings for the <see cref="StatsCommand"/>.
    /// </summary>
    internal sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the .sln, .slnx, or .csproj file to be analyzed.
        /// </summary>
        [CommandArgument(0, "[path]")]
        [Description("The path to the .sln, .slnx, or .csproj file")]
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of hotspot projects to display.
        /// </summary>
        [CommandOption("--top <n>")]
        [Description("Number of most-referenced projects to show (default 5)")]
        [DefaultValue(5)]
        public int Top { get; init; } = 5;

        /// <inheritdoc />
        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return ValidationResult.Error("Path is required");
            }

            if (Top < 1)
            {
                return ValidationResult.Error("--top must be at least 1");
            }

            return ValidationResult.Success();
        }
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!fileSystem.FileExists(settings.Path))
            {
                console.WriteError($"File not found: {settings.Path}");
                return 1;
            }

            var extension = fileSystem.GetExtension(settings.Path);
            if (!extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                console.WriteError("File must be a .sln, .slnx, or .csproj file.");
                return 1;
            }

            var sw = Stopwatch.StartNew();
            var stats = await statsService.ComputeStatsAsync(settings.Path, settings.Top, cancellationToken);
            sw.Stop();

            // ── Header ────────────────────────────────────────────────────────
            SpectreOutputConsole.Write(new Rule($"[bold blue]{stats.SolutionName}[/]").LeftJustified());

            // ── Metrics table ─────────────────────────────────────────────────
            var table = new Table
            {
                Border = TableBorder.None,
                ShowHeaders = false
            };

            table.AddColumn(new TableColumn("Metric").Width(30))
                .AddColumn(new TableColumn("Value").RightAligned())
                .AddRow("[grey]Total projects[/]", $"[bold]{stats.TotalProjectCount}[/]")
                .AddRow("  Libraries", $"{stats.TypeBreakdown["Library"]}")
                .AddRow("  Executables", $"{stats.TypeBreakdown["Executable"]}")
                .AddRow("  Test projects", $"{stats.TypeBreakdown["Test"]}")
                .AddRow("  Other", $"{stats.TypeBreakdown["Other"]}");

            if (stats.HasCycles)
            {
                table.AddRow("[grey]Dependency depth[/]", "[red]N/A (cycles detected)[/]");
            }
            else
            {
                table
                    .AddRow("[grey]Average depth[/]", $"{stats.DepthStats.Average:0.##}")
                    .AddRow("[grey]Min depth[/]", $"{stats.DepthStats.Min}")
                    .AddRow("[grey]Max depth[/]", $"{stats.DepthStats.Max}");
            }

            table
                .AddRow("[grey]Cycles detected[/]", stats.HasCycles ? "[red]Yes[/]" : "[green]No[/]")
                .AddRow("[grey]Analysis time[/]", $"{sw.ElapsedMilliseconds} ms");

            SpectreOutputConsole.Write(table);

            // ── Hotspot projects ──────────────────────────────────────────────
            if (stats.HotspotProjects.Count > 0)
            {
                console.WriteMarkup("\n[grey]Most-referenced projects:[/]");
                for (var i = 0; i < stats.HotspotProjects.Count; i++)
                {
                    var h = stats.HotspotProjects[i];
                    console.WriteMarkup($"  [bold]{i + 1,2}.[/] {Markup.Escape(h.Name),-40} \u2190 {h.InDegree}");
                }
            }

            console.WriteLine(string.Empty);

            return 0;
        }
#pragma warning disable CA1031 // CLI handler intentionally catches all exceptions for user-friendly display
        catch (Exception ex)
#pragma warning restore CA1031
        {
            console.WriteError(ex.Message);
            return 1;
        }
    }
}
