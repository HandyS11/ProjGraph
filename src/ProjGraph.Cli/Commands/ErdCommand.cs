using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global
#pragma warning disable CA1812 // Types are instantiated by Spectre.Console DI via reflection

namespace ProjGraph.Cli.Commands;

/// <summary>
/// Represents the command for generating an Entity Relationship Diagram (ERD) from a DbContext or ModelSnapshot file.
/// </summary>
/// <remarks>
/// The <see cref="ErdCommand"/> class is an asynchronous command that utilizes the `Settings` class
/// to configure the path to the DbContext/ModelSnapshot file and the optional name. It processes the input
/// and generates a Mermaid ERD diagram based on the analyzed Entity Framework model.
/// </remarks>
/// <param name="efService">The Entity Framework analysis service for discovering and analyzing contexts and snapshots.</param>
/// <param name="mermaidRenderer">The diagram renderer for producing Mermaid ERD output.</param>
/// <param name="console">The output console for writing results and errors.</param>
/// <param name="fileSystem">The file system abstraction for disk operations.</param>
internal sealed class ErdCommand(
    IEfAnalysisService efService,
    IDiagramRenderer<EfModel> mermaidRenderer,
    IOutputConsole console,
    IFileSystem fileSystem)
    : AsyncCommand<ErdCommand.Settings>
{
    /// <summary>
    /// Represents the settings for the `ErdCommand`.
    /// </summary>
    /// <remarks>
    /// This class contains the configuration options for the `ErdCommand`, including the path to the input file
    /// and the optional context/snapshot name. It also provides validation for the input settings.
    /// </remarks>
    internal sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to a .cs file containing a DbContext or ModelSnapshot.
        /// If not specified, the current directory will be searched for relevant files.
        /// </summary>
        [CommandArgument(0, "[path]")]
        [Description(
            "Path to a .cs file containing a DbContext or ModelSnapshot (optional, searches current directory if not specified)")]
        public string? Path { get; init; }

        /// <summary>
        /// Gets or sets the name of the DbContext or ModelSnapshot to analyze.
        /// This is an optional parameter.
        /// </summary>
        [CommandOption("-c|--context")]
        [Description("The name of the DbContext or ModelSnapshot to analyze (optional)")]
        public string? ContextName { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include the title in the rendered output.
        /// </summary>
        [CommandOption("--show-title <true|false>")]
        [Description("Include the diagram title (default true)")]
        [DefaultValue(true)]
        public bool ShowTitle { get; init; } = true;

        /// <summary>
        /// Gets or sets the output file path.
        /// If specified, the diagram will be written to this file instead of stdout.
        /// </summary>
        [CommandOption("-o|--output <path>")]
        [Description("The output file path")]
        public string? Output { get; init; }

        /// <summary>
        /// Validates the settings provided for the command.
        /// Ensures that the specified path exists, is a .cs file, or is left empty to search the current directory.
        /// </summary>
        /// <returns>
        /// A <see cref="ValidationResult"/> indicating whether the settings are valid.
        /// </returns>
        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return ValidationResult.Success();
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
    /// Executes the command asynchronously, analyzing the specified DbContext or ModelSnapshot file and generating a Mermaid ERD diagram.
    /// </summary>
    /// <param name="context">
    /// The command context containing information about the execution environment.
    /// </param>
    /// <param name="settings">
    /// The settings for the command, including the path to the input file and the optional name.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// An integer representing the exit code of the command. Returns 0 if successful, 1 otherwise.
    /// </returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetPath = await ResolveTargetPathAsync(settings.Path, console, cancellationToken);
            if (targetPath is null)
            {
                return 1;
            }

            var model = await AnalyzeModelAsync(targetPath, settings.ContextName, cancellationToken);

            var wrapInMarkdownFence = settings.Output?.EndsWith(".mmd", StringComparison.OrdinalIgnoreCase) is false;

            var mermaidOutput =
                mermaidRenderer.Render(model, new DiagramOptions(settings.ShowTitle, wrapInMarkdownFence));

            if (settings.Output is not null)
            {
                var directory = fileSystem.GetDirectoryName(settings.Output);
                if (!string.IsNullOrEmpty(directory))
                {
                    fileSystem.CreateDirectory(directory);
                }

                await fileSystem.WriteAllTextAsync(settings.Output, mermaidOutput, cancellationToken);
                console.WriteInfo($"Saved to {settings.Output}");
            }
            else
            {
                console.WriteLine(mermaidOutput);
            }

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

    /// <summary>
    /// Resolves the target file path, either from the provided path or by discovering files in the current directory.
    /// </summary>
    /// <param name="providedPath">The path provided by the user, or null to search automatically.</param>
    /// <param name="console">The output console for user interaction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved file path, or null if no valid file was found.</returns>
    private static async Task<string?> ResolveTargetPathAsync(string? providedPath, IOutputConsole console,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(providedPath))
        {
            return providedPath;
        }

        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), $"*DbContext{FilePathGuard.CSharpExtension}",
                SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Directory.GetCurrentDirectory(),
                $"*ModelSnapshot{FilePathGuard.CSharpExtension}",
                SearchOption.AllDirectories))
            .ToList();

        return files.Count switch
        {
            0 => HandleNoFilesFound(console),
            1 => HandleSingleFileFound(files[0], console),
            _ => await HandleMultipleFilesFoundAsync(files, console, cancellationToken)
        };
    }

    /// <summary>
    /// Handles the case when no DbContext or ModelSnapshot files are found.
    /// </summary>
    /// <param name="console">The output console for user interaction.</param>
    /// <returns>Null to indicate failure.</returns>
    private static string? HandleNoFilesFound(IOutputConsole console)
    {
        console.WriteError("No DbContext or ModelSnapshot .cs file found.");
        console.WriteMarkup("[grey]Usage: projgraph erd path/to/YourDbContext.cs[/]");
        return null;
    }

    /// <summary>
    /// Handles the case when a single file is found automatically.
    /// </summary>
    /// <param name="filePath">The path to the found file.</param>
    /// <param name="console">The output console for user interaction.</param>
    /// <returns>The file path.</returns>
    private static string HandleSingleFileFound(string filePath, IOutputConsole console)
    {
        console.WriteMarkup($"[grey]Using [white]{Path.GetFileName(filePath)}[/]...[/]");
        return filePath;
    }

    /// <summary>
    /// Handles the case when multiple files are found, prompting the user to select one.
    /// </summary>
    /// <param name="files">The list of found files.</param>
    /// <param name="console">The output console for user interaction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The selected file path.</returns>
    private static async Task<string> HandleMultipleFilesFoundAsync(List<string> files,
        IOutputConsole console, CancellationToken cancellationToken)
    {
        var selectedFileName = await console.PromptSelectionAsync(
            "Multiple files found. Please select one:",
            files.Select(f => Path.GetFileName(f)),
            cancellationToken);
        return files.First(f => Path.GetFileName(f) == selectedFileName);
    }

    /// <summary>
    /// Analyzes the Entity Framework model from the specified file.
    /// </summary>
    /// <param name="targetPath">The path to the file to analyze.</param>
    /// <param name="contextName">The optional context or snapshot name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The analyzed EF model.</returns>
    private async Task<EfModel> AnalyzeModelAsync(
        string targetPath,
        string? contextName,
        CancellationToken cancellationToken)
    {
        if (targetPath.EndsWith($"ModelSnapshot{FilePathGuard.CSharpExtension}", StringComparison.OrdinalIgnoreCase))
        {
            return await AnalyzeSnapshotAsync(targetPath, contextName, cancellationToken);
        }

        return await AnalyzeContextAsync(targetPath, contextName, cancellationToken);
    }

    /// <summary>
    /// Analyzes a ModelSnapshot file.
    /// </summary>
    /// <param name="targetPath">The path to the snapshot file.</param>
    /// <param name="contextName">The optional snapshot name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The analyzed EF model.</returns>
    private async Task<EfModel> AnalyzeSnapshotAsync(
        string targetPath,
        string? contextName,
        CancellationToken cancellationToken)
    {
        var snapshots = await efService.DiscoverSnapshotsAsync(targetPath);
        var selectedSnapshot = await SelectItemAsync(
            snapshots,
            contextName,
            "Multiple ModelSnapshots found. Please select one:",
            $"No ModelSnapshot found in '{targetPath}'.",
            console,
            cancellationToken);

        return await efService.AnalyzeSnapshotAsync(targetPath, selectedSnapshot);
    }

    /// <summary>
    /// Analyzes a DbContext file.
    /// </summary>
    /// <param name="targetPath">The path to the context file.</param>
    /// <param name="contextName">The optional context name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The analyzed EF model.</returns>
    private async Task<EfModel> AnalyzeContextAsync(
        string targetPath,
        string? contextName,
        CancellationToken cancellationToken)
    {
        var contexts = await efService.DiscoverContextsAsync(targetPath);
        var selectedContext = await SelectItemAsync(
            contexts,
            contextName,
            "Multiple DbContexts found. Please select one:",
            $"No DbContext found in '{targetPath}'.",
            console,
            cancellationToken);

        return await efService.AnalyzeContextAsync(targetPath, selectedContext);
    }

    /// <summary>
    /// Selects an item from a list, either using the provided name or prompting the user.
    /// </summary>
    /// <param name="items">The list of available items.</param>
    /// <param name="providedName">The optional pre-selected item name.</param>
    /// <param name="promptTitle">The title to display when prompting the user.</param>
    /// <param name="notFoundMessage">The error message when no items are found.</param>
    /// <param name="console">The output console for user interaction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The selected item name.</returns>
    /// <exception cref="AnalysisException">Thrown when no items are found in the list.</exception>
    private static async Task<string> SelectItemAsync(
        List<string> items,
        string? providedName,
        string promptTitle,
        string notFoundMessage,
        IOutputConsole console,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(providedName))
        {
            return providedName;
        }

        return items.Count switch
        {
            0 => throw new AnalysisException(notFoundMessage),
            > 1 => await console.PromptSelectionAsync(promptTitle, items, cancellationToken),
            _ => items[0]
        };
    }
}
