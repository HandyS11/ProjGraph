using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ProjGraph.Core.Exceptions;
using ProjGraph.Lib.Core.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse solution files and extract project paths.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
public sealed class SlnParser(IFileSystem fileSystem) : ISlnParser
{
    /// <summary>
    /// The project type GUIDs that MSBuild's <c>SolutionFile</c> classifies as
    /// <c>KnownToBeMSBuildFormat</c>: C#, Visual Basic, and F# (classic and SDK-style), the generic
    /// CPS project, C++, and the legacy database, J#, and Synergy project types. Every other type
    /// (solution folders, shared, website, and unrecognised projects) is skipped.
    /// </summary>
    private static readonly HashSet<Guid> MsBuildProjectTypes =
    [
        new("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"), // C#
        new("9A19103F-16F7-4668-BE54-9A1E7A4F7556"), // C# (SDK-style)
        new("F184B08F-C81C-45F6-A57F-5ABD9991F28F"), // Visual Basic
        new("778DAE3C-4631-46EA-AA77-85C1314464D9"), // Visual Basic (SDK-style)
        new("F2A71F9B-5D33-465A-A702-920D77279786"), // F#
        new("6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705"), // F# (SDK-style)
        new("13B669BE-BB05-4DDF-9536-439F39A36129"), // CPS (generic SDK-style)
        new("8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942"), // C++
        new("C8D11400-126E-41CD-887F-60BD40844F9E"), // Database
        new("E6FDF86B-F3D1-11D4-8576-0002A516ECE8"), // J#
        new("BBD0F5D1-1CC4-42FD-BA4C-A96779C64378") // Synergy
    ];

    /// <summary>
    /// Retrieves the paths of all projects in the specified solution file.
    /// </summary>
    /// <param name="path">The file path to the solution file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the solution.
    /// If the solution file does not exist, an empty collection is returned.
    /// </returns>
    /// <exception cref="ParsingException">Thrown when the solution file cannot be parsed.</exception>
    public IEnumerable<string> GetProjectPaths(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        var fullPath = fileSystem.GetFullPath(path);

        SolutionModel solution;
        try
        {
            solution = OpenSolution(fileSystem.ReadAllText(fullPath));
        }
        catch (Exception ex) when (ex is SolutionException or IOException or UnauthorizedAccessException)
        {
            throw new ParsingException($"Failed to read or parse .sln file: {path}", ex);
        }

        var solutionDirectory = fileSystem.GetDirectoryName(fullPath) ?? "";
        return [.. solution.SolutionProjects
            .Where(p => MsBuildProjectTypes.Contains(p.TypeId))
            .Select(p => fileSystem.GetFullPath(fileSystem.Combine(solutionDirectory,
                p.FilePath.Replace('\\', Path.DirectorySeparatorChar))))];
    }

    /// <summary>
    /// Parses solution text with the <c>.sln</c> serializer.
    /// </summary>
    /// <param name="content">The solution file text.</param>
    /// <returns>The parsed solution model.</returns>
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits",
        Justification = "The serializer reads an in-memory stream, so the wait never blocks on I/O, and " +
                        "neither the CLI nor the MCP server calls this on a thread with a synchronization " +
                        "context. ISlnParser stays synchronous until the planned CancellationToken work.")]
    private static SolutionModel OpenSolution(string content)
    {
        // Not disposed: a MemoryStream over a byte array holds no unmanaged resources, and disposing it
        // around the bridged call trips CA2025 even though GetResult completes the read first.
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false);
        return SolutionSerializers.SlnFileV12.OpenAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
    }
}
