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
    private static CSharpCompilation CreateCSharpCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var references = BuildMetadataReferences();
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
    /// This method initializes a list of metadata references with essential assemblies such as `System.Object`,
    /// `System.Collections.Generic.IEnumerable`, `System.Collections.Generic.ICollection`, and `System.Runtime`.
    /// It also attempts to add additional references for `System.Collections`, `System.ComponentModel.Annotations`
    /// or `System.ComponentModel.DataAnnotations`, and `Microsoft.EntityFrameworkCore`.
    /// </remarks>
    private static List<MetadataReference> BuildMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ICollection<>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        TryAddReference(references, "System.Collections");
        TryAddDataAnnotationsReference(references);
        TryAddEntityFrameworkCoreReference(references);

        return references;
    }

    /// <summary>
    /// Attempts to add a metadata reference for the specified assembly to the provided list of references.
    /// </summary>
    /// <param name="references">The list of metadata references to which the assembly reference will be added.</param>
    /// <param name="assemblyName">The name of the assembly to load and add as a metadata reference.</param>
    /// <remarks>
    /// If the specified assembly cannot be loaded or an error occurs, the method fails silently as the reference is not critical.
    /// </remarks>
    private static void TryAddReference(List<MetadataReference> references, string assemblyName)
    {
        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load(assemblyName).Location));
        }
        catch
        {
            // Not critical if not found
        }
    }

    /// <summary>
    /// Attempts to add a metadata reference for the Data Annotations assembly to the provided list of references.
    /// </summary>
    /// <param name="references">The list of metadata references to which the Data Annotations reference will be added.</param>
    /// <remarks>
    /// This method first tries to load the "System.ComponentModel.Annotations" assembly. 
    /// If it fails, it attempts to load the "System.ComponentModel.DataAnnotations" assembly instead. 
    /// If both attempts fail, the method fails silently as the reference is not critical.
    /// </remarks>
    private static void TryAddDataAnnotationsReference(List<MetadataReference> references)
    {
        try
        {
            references.Add(
                MetadataReference.CreateFromFile(
                    Assembly.Load("System.ComponentModel.Annotations").Location));
        }
        catch
        {
            try
            {
                references.Add(
                    MetadataReference.CreateFromFile(
                        Assembly.Load("System.ComponentModel.DataAnnotations").Location));
            }
            catch
            {
                // Not critical if not found
            }
        }
    }

    /// <summary>
    /// Attempts to add a metadata reference for the Microsoft.EntityFrameworkCore assembly to the provided list of references.
    /// </summary>
    /// <param name="references">The list of metadata references to which the Entity Framework Core reference will be added.</param>
    private static void TryAddEntityFrameworkCoreReference(List<MetadataReference> references)
    {
        try
        {
            var efCoreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Microsoft.EntityFrameworkCore");

            if (efCoreAssembly != null)
            {
                references.Add(MetadataReference.CreateFromFile(efCoreAssembly.Location));
            }
        }
        catch
        {
            // Not critical if not found
        }
    }
}



