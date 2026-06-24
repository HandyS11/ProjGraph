using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.EntityFramework.Application.UseCases;

/// <summary>
/// Use case for discovering all DbContext classes within a C# file.
/// </summary>
/// <param name="modelAnalyzer">The EF model analyzer used for DbContext discovery.</param>
/// <param name="fileSystem">The file system abstraction for reading source files.</param>
public class DiscoverContextsUseCase(IEfModelAnalyzer modelAnalyzer, IFileSystem fileSystem)
{
    /// <summary>
    /// Executes the discovery of all DbContext classes within a specified C# file.
    /// </summary>
    /// <param name="path">The file path to the C# source file to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of discovered DbContext class names.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided file path is not a .cs file.</exception>
    public async Task<List<string>> ExecuteAsync(string path)
    {
        FilePathGuard.RequireCsFile(path);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            await fileSystem.ReadAllTextAsync(path));
        var root = await syntaxTree.GetRootAsync();

        return [.. modelAnalyzer.DiscoverDbContexts(root)];
    }
}
