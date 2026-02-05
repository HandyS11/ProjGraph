using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Lib.Application.Interfaces;

namespace ProjGraph.Lib.Application.UseCases.EfAnalysis;

/// <summary>
/// Use case for discovering all ModelSnapshot classes within a C# file.
/// </summary>
public class DiscoverSnapshotsUseCase(IEfModelAnalyzer modelAnalyzer)
{
    /// <summary>
    /// Executes the discovery of all ModelSnapshot classes within a specified C# file.
    /// </summary>
    /// <param name="path">The file path to the C# source file to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of discovered ModelSnapshot class names.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided file path is not a .cs file.</exception>
    public async Task<List<string>> ExecuteAsync(string path)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .cs files are supported", nameof(path));
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(path));
        var root = await syntaxTree.GetRootAsync();

        return [.. modelAnalyzer.DiscoverModelSnapshots(root)];
    }
}