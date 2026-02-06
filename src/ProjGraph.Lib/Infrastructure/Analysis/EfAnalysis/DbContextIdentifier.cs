using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Constants;

namespace ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;

/// <summary>
/// Provides methods to identify and find DbContext classes within a collection of class declarations.
/// </summary>
public static class DbContextIdentifier
{
    /// <summary>
    /// Determines whether the specified class declaration represents a DbContext class.
    /// </summary>
    /// <param name="class">The class declaration to check.</param>
    /// <returns>
    /// <c>true</c> if the class declaration has a base type that contains "DbContext"; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsDbContext(ClassDeclarationSyntax @class)
    {
        return @class.BaseList?.Types.Any(t => t.ToString().Contains(EfAnalysisConstants.CommonNames.DbContext)) ??
               false;
    }

    /// <summary>
    /// Determines whether the specified class declaration represents a ModelSnapshot class.
    /// </summary>
    /// <param name="class">The class declaration to check.</param>
    /// <returns>
    /// <c>true</c> if the class declaration has a base type that contains "ModelSnapshot"; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsModelSnapshot(ClassDeclarationSyntax @class)
    {
        return @class.BaseList?.Types.Any(t => t.ToString().Contains(EfAnalysisConstants.CommonNames.ModelSnapshot)) ??
               false;
    }

    /// <summary>
    /// Finds a specific DbContext class by name or returns the first DbContext class in the collection.
    /// </summary>
    /// <param name="classDeclarations">A collection of class declarations to search.</param>
    /// <param name="contextName">The name of the DbContext class to find. If <c>null</c>, the first DbContext class is returned.</param>
    /// <returns>
    /// The <see cref="ClassDeclarationSyntax"/> of the matching DbContext class, or <c>null</c> if no match is found.
    /// </returns>
    public static ClassDeclarationSyntax? FindContextClass(
        IEnumerable<ClassDeclarationSyntax> classDeclarations,
        string? contextName)
    {
        return classDeclarations.FirstOrDefault(c =>
            (contextName is null && IsDbContext(c)) || c.Identifier.Text == contextName);
    }

    /// <summary>
    /// Finds a specific ModelSnapshot class by name or returns the first ModelSnapshot class in the collection.
    /// </summary>
    /// <param name="classDeclarations">A collection of class declarations to search.</param>
    /// <param name="snapshotName">The name of the ModelSnapshot class to find. If <c>null</c>, the first ModelSnapshot class is returned.</param>
    /// <returns>
    /// The <see cref="ClassDeclarationSyntax"/> of the matching ModelSnapshot class, or <c>null</c> if no match is found.
    /// </returns>
    public static ClassDeclarationSyntax? FindSnapshotClass(
        IEnumerable<ClassDeclarationSyntax> classDeclarations,
        string? snapshotName)
    {
        return classDeclarations.FirstOrDefault(c =>
            (snapshotName is null && IsModelSnapshot(c)) || c.Identifier.Text == snapshotName);
    }
}

