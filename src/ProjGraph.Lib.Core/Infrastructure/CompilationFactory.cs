using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Lib.Core.Abstractions;
using System.Reflection;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Provides a factory for creating Roslyn <see cref="Compilation"/> objects.
/// </summary>
public sealed class CompilationFactory : ICompilationFactory
{
    /// <summary>
    /// The manifest resource name prefix under which the reference assemblies are embedded.
    /// </summary>
    private const string ReferenceResourcePrefix = "refs/";

    /// <summary>
    /// The BCL/EF metadata reference set, built once and reused across compilations: it is
    /// immutable for the process lifetime and building it copies several embedded assemblies.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> CachedReferences =
        new(BuildMetadataReferences);

    /// <summary>
    /// Creates a new Roslyn <see cref="CSharpCompilation"/> object using the provided syntax trees and necessary metadata references.
    /// </summary>
    /// <param name="syntaxTrees">A collection of <see cref="SyntaxTree"/> objects to include in the compilation.</param>
    /// <returns>
    /// A <see cref="CSharpCompilation"/> object that represents the compiled code from the provided syntax trees.
    /// </returns>
    /// <remarks>
    /// This method generates a new C# compilation named "AdHoc" by adding the provided syntax trees and
    /// a set of metadata references built using the <see cref="BuildMetadataReferences"/> method.
    /// </remarks>
    public Compilation CreateCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        return CreateCSharpCompilation(syntaxTrees);
    }

    /// <summary>
    /// Creates a new Roslyn <see cref="CSharpCompilation"/> object.
    /// </summary>
    /// <param name="syntaxTrees">The syntax trees to compile.</param>
    private static CSharpCompilation CreateCSharpCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var references = CachedReferences.Value;
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable);

        return CSharpCompilation.Create("AdHoc")
            .WithOptions(options)
            .AddReferences(references)
            .AddSyntaxTrees(syntaxTrees);
    }

    /// <summary>
    /// Builds a list of metadata references required for Roslyn compilation.
    /// </summary>
    /// <returns>A list of <see cref="MetadataReference"/> objects representing the necessary references.</returns>
    /// <remarks>
    /// The BCL references come from the reference assemblies embedded in this library under the
    /// <c>refs/</c> resource prefix (<c>System.Runtime</c>, <c>System.Collections</c>,
    /// <c>System.ComponentModel.Annotations</c>, and <c>netstandard</c>), so the reference set is
    /// the same whether the host runs on the JIT runtime or as a Native AOT executable, where
    /// <see cref="Assembly.Location"/> is always empty. <c>Microsoft.EntityFrameworkCore</c> is
    /// added when it is loaded from a file.
    /// </remarks>
    private static List<MetadataReference> BuildMetadataReferences()
    {
        var assembly = typeof(CompilationFactory).Assembly;
        var references = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ReferenceResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => CreateEmbeddedReference(assembly, name))
            .ToList<MetadataReference>();

        TryAddEntityFrameworkCoreReference(references);

        return references;
    }

    /// <summary>
    /// Creates a metadata reference from an embedded reference assembly.
    /// </summary>
    /// <param name="assembly">The assembly that embeds the resource.</param>
    /// <param name="resourceName">The manifest resource name, which also becomes the reference's display name.</param>
    /// <returns>The metadata reference.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource cannot be opened.</exception>
    private static PortableExecutableReference CreateEmbeddedReference(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded reference assembly '{resourceName}' could not be opened.");
        return MetadataReference.CreateFromStream(stream, filePath: resourceName);
    }

    /// <summary>
    /// Attempts to add a metadata reference for the Microsoft.EntityFrameworkCore assembly to the provided list of references.
    /// </summary>
    /// <param name="references">The list of metadata references to which the Entity Framework Core reference will be added.</param>
    /// <remarks>
    /// The assembly is skipped when its <see cref="Assembly.Location"/> is empty, which is always
    /// the case under Native AOT, instead of passing an empty path to
    /// <see cref="MetadataReference.CreateFromFile(string, MetadataReferenceProperties, DocumentationProvider?)"/>.
    /// </remarks>
    private static void TryAddEntityFrameworkCoreReference(List<MetadataReference> references)
    {
        try
        {
            var efCoreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Microsoft.EntityFrameworkCore");

            if (efCoreAssembly is { Location.Length: > 0 })
            {
                references.Add(MetadataReference.CreateFromFile(efCoreAssembly.Location));
            }
        }
        catch (FileNotFoundException)
        {
            // Not critical if not found
        }
    }
}
