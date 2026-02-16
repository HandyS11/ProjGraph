using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global
#pragma warning disable CA1812 // Types are instantiated by Spectre.Console DI via reflection

namespace ProjGraph.Cli.Commands;

/// <summary>
/// Represents a command that generates a class diagram from a specified .cs file.
/// Inherits from <see cref="AsyncCommand{TSettings}"/> with <see cref="ClassDiagramCommand.Settings"/> as the settings type.
/// </summary>
/// <param name="analysisService">The class analysis service used to analyze C# files.</param>
/// <param name="mermaidRenderer">The diagram renderer for producing Mermaid class diagram output.</param>
/// <param name="console">The output console for writing results and errors.</param>
internal sealed class ClassDiagramCommand(
    IClassAnalysisService analysisService,
    IDiagramRenderer<ClassModel> mermaidRenderer,
    IOutputConsole console)
    : AsyncCommand<ClassDiagramCommand.Settings>
{
    /// <summary>
    /// Represents the settings for the ClassDiagramCommand.
    /// </summary>
    internal sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the .cs file to analyze.
        /// </summary>
        /// <value>The file path as a string.</value>
        [CommandArgument(0, "<path>")]
        [Description("Path to the .cs file to analyze.")]
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to include base classes and interfaces in the analysis.
        /// </summary>
        /// <value>True to include inheritance; otherwise, false.</value>
        [CommandOption("-i|--inheritance")]
        [Description("Discover base classes and interfaces in the workspace (optional)")]
        [DefaultValue(false)]
        public bool IncludeInheritance { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include dependent types in the analysis.
        /// </summary>
        /// <value>True to include dependencies; otherwise, false.</value>
        [CommandOption("-d|--dependencies")]
        [Description("Discover dependent types in the workspace (optional)")]
        [DefaultValue(false)]
        public bool IncludeDependencies { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include properties and fields in the diagram.
        /// </summary>
        [CommandOption("--properties <true|false>")]
        [Description("Show properties and fields in the diagram (default true)")]
        [DefaultValue(true)]
        public bool IncludeProperties { get; init; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include functions/methods in the diagram.
        /// </summary>
        [CommandOption("--functions <true|false>")]
        [Description("Show functions and methods in the diagram (default true)")]
        [DefaultValue(true)]
        public bool IncludeFunctions { get; init; } = true;

        /// <summary>
        /// Gets or sets the maximum depth for relationship discovery.
        /// </summary>
        /// <value>An integer representing the maximum depth. Default is 1.</value>
        [CommandOption("--depth")]
        [Description("Max depth for relationship discovery (default 1)")]
        [DefaultValue(1)]
        public int Depth { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include the title in the rendered output.
        /// </summary>
        [CommandOption("--show-title <true|false>")]
        [Description("Include the diagram title (default true)")]
        [DefaultValue(true)]
        public bool ShowTitle { get; init; } = true;

        /// <summary>
        /// Validates the settings provided by the user.
        /// Ensures the file path is valid, exists, and points to a .cs file.
        /// </summary>
        /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return ValidationResult.Error("Path is required");
            }

            if (!File.Exists(Path))
            {
                return ValidationResult.Error($"File not found: {Path}");
            }

            if (!Path.EndsWith(FilePathGuard.CSharpExtension, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Error($"Only .cs files are supported. Got: {Path}");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Executes the ClassDiagramCommand asynchronously.
    /// </summary>
    /// <param name="context">The command context containing metadata about the execution environment.</param>
    /// <param name="settings">The settings provided by the user for the command execution.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an integer
    /// indicating the exit code of the command (0 for success, 1 for failure).
    /// </returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await analysisService.AnalyzeFileAsync(
                settings.Path,
                settings.IncludeInheritance,
                settings.IncludeDependencies,
                settings.IncludeProperties,
                settings.IncludeFunctions,
                settings.Depth);

            var mermaidOutput = mermaidRenderer.Render(model, new DiagramOptions(settings.ShowTitle));
            console.WriteLine(mermaidOutput);

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception type — CLI handler intentionally catches all for user-friendly display
        catch (Exception ex)
#pragma warning restore CA1031
        {
            console.WriteError(ex.Message);
            return 1;
        }
    }
}
