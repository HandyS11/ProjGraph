using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Interface for advanced analysis of Entity Framework models, including discovery and analysis of DbContext and ModelSnapshot classes.
/// </summary>
public interface IEfModelAnalyzer
{
    /// <summary>
    /// Discovers DbContext classes in a given syntax tree.
    /// </summary>
    /// <param name="root">The root syntax node of the syntax tree to analyze.</param>
    /// <returns>A collection of strings representing the names of discovered DbContext classes.</returns>
    IEnumerable<string> DiscoverDbContexts(SyntaxNode root);

    /// <summary>
    /// Discovers ModelSnapshot classes in a given syntax tree.
    /// </summary>
    /// <param name="root">The root syntax node of the syntax tree to analyze.</param>
    /// <returns>A collection of strings representing the names of discovered ModelSnapshot classes.</returns>
    IEnumerable<string> DiscoverModelSnapshots(SyntaxNode root);

    /// <summary>
    /// Analyzes a ModelSnapshot class and its related files to extract Entity Framework model information.
    /// </summary>
    /// <param name="snapshotPath">The file path to the ModelSnapshot class.</param>
    /// <param name="snapshotName">The optional name of the ModelSnapshot class to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    Task<EfModel> AnalyzeSnapshotAsync(string snapshotPath, string? snapshotName);

    /// <summary>
    /// Analyzes a DbContext class and its related entity files to extract Entity Framework model information.
    /// </summary>
    /// <param name="path">The file path to the DbContext class.</param>
    /// <param name="contextName">The optional name of the DbContext class to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    Task<EfModel> AnalyzeContextAsync(string path, string? contextName);
}