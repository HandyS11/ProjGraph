using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ProjGraph.Tests.Shared.Helpers;

/// <summary>
/// Provides helper methods for creating Roslyn compilations from C# source snippets.
/// Used by unit tests that need to test Roslyn-based analyzers and infrastructure.
/// </summary>
public static class RoslynTestHelper
{
    /// <summary>
    /// The default set of metadata references required for basic compilations.
    /// Includes the core runtime assemblies and common BCL types.
    /// </summary>
    private static readonly MetadataReference[] DefaultReferences = GetDefaultReferences();

    /// <summary>
    /// Creates a <see cref="CSharpCompilation"/> from one or more C# source code strings.
    /// The compilation includes references to essential BCL assemblies.
    /// </summary>
    /// <param name="sources">One or more C# source code strings.</param>
    /// <returns>A <see cref="CSharpCompilation"/> ready for semantic analysis.</returns>
    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Test{i}.cs"));

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            DefaultReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a compilation and returns the <see cref="SemanticModel"/> for the first source file.
    /// </summary>
    /// <param name="source">The C# source code.</param>
    /// <returns>A tuple of the compilation and the semantic model for the source.</returns>
    public static (CSharpCompilation Compilation, SemanticModel SemanticModel) CreateCompilationWithModel(
        string source)
    {
        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees[0];
        var model = compilation.GetSemanticModel(tree);
        return (compilation, model);
    }

    /// <summary>
    /// Gets the <see cref="INamedTypeSymbol"/> for a type with the given name from a compilation.
    /// </summary>
    /// <param name="compilation">The compilation to search.</param>
    /// <param name="typeName">The fully qualified or simple name of the type.</param>
    /// <returns>The resolved type symbol, or null if not found.</returns>
    public static INamedTypeSymbol? GetTypeSymbol(CSharpCompilation compilation, string typeName)
    {
        return compilation.GetTypeByMetadataName(typeName)
               ?? compilation.GlobalNamespace
                   .GetMembers()
                   .OfType<INamedTypeSymbol>()
                   .FirstOrDefault(t => t.Name == typeName)
               ?? compilation.GlobalNamespace
                   .GetNamespaceMembers()
                   .SelectMany(ns => ns.GetTypeMembers())
                   .FirstOrDefault(t => t.Name == typeName);
    }

    public static MetadataReference[] GetDefaultReferences()
    {
        // Get the runtime directory to resolve core assemblies
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Linq.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.ComponentModel.Annotations.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll"))
        ];
    }
}
