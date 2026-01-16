using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Interfaces;
using System.Diagnostics;
using System.Reflection;

namespace ProjGraph.Lib.Services;

public class EfAnalysisService : IEfAnalysisService
{
    private static readonly ActivitySource ActivitySource = new("ProjGraph.Lib.EfAnalysis");

    public async Task<List<string>> DiscoverContextsAsync(string path)
    {
        using var activity = ActivitySource.StartActivity("DiscoverContexts");
        activity?.SetTag("path", path);
        var contexts = new List<string>();

        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(path));
            var root = await syntaxTree.GetRootAsync();
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var @class in classDeclarations)
            {
                if (IsDbContext(@class))
                {
                    contexts.Add(@class.Identifier.Text);
                }
            }
        }
        else
        {
            using var workspace = MSBuildWorkspace.Create();
            Solution solution;
            if (path.EndsWith(".sln") || path.EndsWith(".slnx"))
            {
                solution = await workspace.OpenSolutionAsync(path);
            }
            else if (path.EndsWith(".csproj"))
            {
                var project = await workspace.OpenProjectAsync(path);
                solution = project.Solution;
            }
            else
            {
                throw new ArgumentException("Unsupported path type", nameof(path));
            }

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                {
                    continue;
                }

                var dbContextSymbol = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");

                foreach (var type in GetNamedTypes(compilation.GlobalNamespace))
                {
                    if (InheritsFrom(type, dbContextSymbol))
                    {
                        contexts.Add(type.Name);
                    }
                }
            }
        }

        return contexts.Distinct().ToList();
    }

    public async Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null)
    {
        using var activity = ActivitySource.StartActivity("AnalyzeContext");
        activity?.SetTag("path", path);
        activity?.SetTag("contextName", contextName);

        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return await AnalyzeFileAsync(path, contextName);
        }

        using var workspace = MSBuildWorkspace.Create();
        Solution solution;
        if (path.EndsWith(".sln") || path.EndsWith(".slnx"))
        {
            solution = await workspace.OpenSolutionAsync(path);
        }
        else
        {
            var project = await workspace.OpenProjectAsync(path);
            solution = project.Solution;
        }

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                continue;
            }

            var dbContextSymbol = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");

            foreach (var type in GetNamedTypes(compilation.GlobalNamespace))
            {
                if (!InheritsFrom(type, dbContextSymbol))
                {
                    continue;
                }

                if (contextName == null || type.Name == contextName)
                {
                    return AnalyzeType(type, compilation);
                }
            }
        }

        throw new Exception($"DbContext {(contextName != null ? $"'{contextName}' " : "")}not found in {path}");
    }

    /// <summary>
    /// Analyzes a single DbContext file by discovering and including entity class files
    /// in the compilation to enable full property extraction from external entity definitions.
    /// </summary>
    private async Task<EfModel> AnalyzeFileAsync(string path, string? contextName)
    {
        var code = await File.ReadAllTextAsync(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // For single file, we need to discover entity files and include them in compilation
        var root = await syntaxTree.GetRootAsync();
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        var contextClass = classDeclarations.FirstOrDefault(c =>
            (contextName == null && IsDbContext(c)) || c.Identifier.Text == contextName);

        if (contextClass == null)
        {
            throw new Exception("DbContext not found in file");
        }

        // Extract using directives to find entity namespaces
        var entityNamespaces = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Name != null && !u.Name.ToString().StartsWith("System") &&
                        !u.Name.ToString().StartsWith("Microsoft"))
            .Select(u => u.Name!.ToString())
            .ToList();

        // Collect entity type names from DbSet properties
        var entityTypeNames = new HashSet<string>();
        foreach (var member in contextClass.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (member.Type is GenericNameSyntax { Identifier.Text: "DbSet" } genericType &&
                genericType.TypeArgumentList.Arguments.Count == 1)
            {
                var entityTypeName = genericType.TypeArgumentList.Arguments[0].ToString();
                entityTypeNames.Add(entityTypeName);
            }
        }

        // Search for entity files in multiple locations
        var contextDirectory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        var searchDirectories = GetEntitySearchDirectories(contextDirectory, entityNamespaces);
        var entityFiles = await DiscoverEntityFilesAsync(searchDirectories, entityTypeNames, path);

        // Create compilation with all discovered entity files
        var syntaxTrees = new List<SyntaxTree> { syntaxTree };
        foreach (var entityFile in entityFiles.Values.Distinct())
        {
            var entityCode = await File.ReadAllTextAsync(entityFile);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(entityCode));
        }

        // Add necessary references for proper type resolution
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ICollection<>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        // Try to add System.Collections reference if available
        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
        }
        catch
        {
            // Not critical if not found
        }

        var compilation = CSharpCompilation.Create("AdHoc")
            .AddReferences(references)
            .AddSyntaxTrees(syntaxTrees);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var contextType = semanticModel.GetDeclaredSymbol(contextClass) as INamedTypeSymbol;
        if (contextType == null)
        {
            throw new Exception("Could not get semantic symbol for context");
        }

        return AnalyzeType(contextType, compilation);
    }

    private static List<string> GetEntitySearchDirectories(string contextDirectory, List<string> entityNamespaces)
    {
        var searchDirs = new List<string> { contextDirectory };

        // Add parent and sibling directories
        var parentDir = Directory.GetParent(contextDirectory);
        if (parentDir == null)
        {
            return searchDirs;
        }

        searchDirs.Add(parentDir.FullName);

        // Search sibling directories that might contain entities
        try
        {
            foreach (var siblingDir in Directory.GetDirectories(parentDir.FullName))
            {
                var dirName = Path.GetFileName(siblingDir);
                // Look for common entity project patterns
                if (dirName.Contains("Entities", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains("Models", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains("Domain", StringComparison.OrdinalIgnoreCase) ||
                    entityNamespaces.Any(ns => ns.Contains(dirName, StringComparison.OrdinalIgnoreCase)))
                {
                    searchDirs.Add(siblingDir);
                }
            }
        }
        catch
        {
            // Ignore access errors
        }

        return searchDirs;
    }

    private static async Task<Dictionary<string, string>> DiscoverEntityFilesAsync(
        List<string> searchDirectories,
        HashSet<string> entityTypeNames,
        string contextFilePath)
    {
        var entityFiles = new Dictionary<string, string>();

        foreach (var searchDir in searchDirectories)
        {
            if (!Directory.Exists(searchDir))
            {
                continue;
            }

            try
            {
                foreach (var csFile in Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories))
                {
                    // Normalize paths for comparison to avoid duplicates
                    if (Path.GetFullPath(csFile).Equals(Path.GetFullPath(contextFilePath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip the DbContext file itself
                    }

                    var fileCode = await File.ReadAllTextAsync(csFile);
                    var fileSyntaxTree = CSharpSyntaxTree.ParseText(fileCode);
                    var fileRoot = await fileSyntaxTree.GetRootAsync();

                    foreach (var classDecl in fileRoot.DescendantNodes().OfType<ClassDeclarationSyntax>())
                    {
                        if (entityTypeNames.Contains(classDecl.Identifier.Text) &&
                            !entityFiles.ContainsKey(classDecl.Identifier.Text))
                        {
                            entityFiles[classDecl.Identifier.Text] = csFile;
                        }
                    }
                }
            }
            catch
            {
                // Ignore access errors for directories we can't read
            }
        }

        return entityFiles;
    }

    private EfModel AnalyzeType(INamedTypeSymbol contextType, Compilation compilation)
    {
        var model = new EfModel { ContextName = contextType.Name };
        var entities = new Dictionary<string, EfEntity>();

        // Find DbSets
        foreach (var member in contextType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.Type is not INamedTypeSymbol { Name: "DbSet", TypeArguments.Length: 1 } typeSymbol)
            {
                continue;
            }

            if (typeSymbol.TypeArguments[0] is INamedTypeSymbol entityType
                && !entities.ContainsKey(entityType.Name))
            {
                entities[entityType.Name] = AnalyzeEntity(entityType);
            }
        }

        model.Entities.AddRange(entities.Values);

        // Analyze Relationships
        var addedRelationships = new HashSet<string>();

        foreach (var entity in entities.Values)
        {
            var symbol = FindSymbolForEntity(entity, compilation);
            if (symbol == null)
            {
                continue;
            }

            foreach (var prop in symbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (!IsNavigationProperty(prop, out var targetType, out var isCollection))
                {
                    continue;
                }

                if (targetType == null || !entities.TryGetValue(targetType.Name, out var targetEntity))
                {
                    continue;
                }

                var relationship = new EfRelationship
                {
                    SourceEntity = entity.Name,
                    TargetEntity = targetEntity.Name,
                    Type = EfRelationshipType.OneToOne, // Default, will be updated below
                    Label = prop.Name,
                    IsRequired = !prop.Type.IsNullable()
                };

                // Determine relationship type
                if (isCollection)
                {
                    // Source has a collection navigation
                    if (HasInverseCollection(prop, targetType))
                    {
                        // Both sides have collections: Many-to-Many
                        relationship.Type = EfRelationshipType.ManyToMany;
                    }
                    else
                    {
                        // Only source has collection: One-to-Many
                        relationship.Type = EfRelationshipType.OneToMany;
                    }
                }
                else
                {
                    // Source has a single reference navigation
                    if (HasInverseCollection(prop, targetType))
                    {
                        // Target has a collection back: this is the "many" side of One-to-Many
                        // We'll let the other side create the relationship
                        // But we should still record it with swapped direction
                        relationship.Type = EfRelationshipType.OneToMany;
                        relationship.SourceEntity = targetEntity.Name;
                        relationship.TargetEntity = entity.Name;
                        relationship.Label = FindInverseCollectionName(prop, targetType) ?? "";
                    }
                    else if (HasInverseReference(prop, targetType))
                    {
                        // Target has a single reference back: One-to-One
                        relationship.Type = EfRelationshipType.OneToOne;
                    }
                    else
                    {
                        // No inverse navigation found: treat as optional One-to-Many
                        relationship.Type = EfRelationshipType.OneToMany;
                        relationship.SourceEntity = targetEntity.Name;
                        relationship.TargetEntity = entity.Name;
                    }
                }

                // Create a normalized key for deduplication
                // For symmetric relationships (1:1, M:M), sort the entity names to avoid duplicates
                string relationshipKey;
                if (relationship.Type == EfRelationshipType.OneToOne ||
                    relationship.Type == EfRelationshipType.ManyToMany)
                {
                    var entities_sorted = new[] { relationship.SourceEntity, relationship.TargetEntity }
                        .OrderBy(e => e)
                        .ToArray();
                    relationshipKey =
                        $"{entities_sorted[0]}-{entities_sorted[1]}-{relationship.Type}";
                }
                else
                {
                    // For OneToMany, direction matters
                    relationshipKey =
                        $"{relationship.SourceEntity}-{relationship.TargetEntity}-{relationship.Type}";
                }

                // Add relationship if not already present
                if (addedRelationships.Add(relationshipKey))
                {
                    model.Relationships.Add(relationship);
                }
            }
        }

        return model;
    }

    private static EfEntity AnalyzeEntity(INamedTypeSymbol type)
    {
        var entity = new EfEntity { Name = type.Name };
        var addedProperties = new HashSet<string>();

        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
        {
            // Skip navigation properties for now in property list
            if (IsNavigationProperty(prop, out _, out _))
            {
                continue;
            }

            // Skip if we've already added this property (prevents duplicates)
            if (!addedProperties.Add(prop.Name))
            {
                continue;
            }

            entity.Properties.Add(new EfProperty
            {
                Name = prop.Name,
                Type = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                IsPrimaryKey = prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                               prop.Name.Equals($"{type.Name}Id", StringComparison.OrdinalIgnoreCase),
                IsForeignKey = prop.Name.EndsWith("Id") && !prop.Name.Equals("Id")
            });
        }

        return entity;
    }

    private static bool IsNavigationProperty(
        IPropertySymbol prop,
        out INamedTypeSymbol? targetType,
        out bool isCollection)
    {
        targetType = null;
        isCollection = false;

        var type = prop.Type;
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Collection detection
        if (namedType.AllInterfaces.Any(i => i.Name == "IEnumerable")
            && namedType.TypeArguments.Length == 1)
        {
            targetType = namedType.TypeArguments[0] as INamedTypeSymbol;
            isCollection = true;
            return targetType != null && IsEntityCandidate(targetType);
        }

        // Reference detection
        targetType = namedType;
        return IsEntityCandidate(targetType);
    }

    private static bool IsEntityCandidate(INamedTypeSymbol type)
    {
        // Simple heuristic: not a primitive, not a string, not in System namespace
        return type.SpecialType == SpecialType.None &&
               type.ContainingNamespace?.Name != "System";
    }

    private static bool HasInverseCollection(IPropertySymbol prop, INamedTypeSymbol targetType)
    {
        return targetType.GetMembers().OfType<IPropertySymbol>().Any(p =>
            IsNavigationProperty(p, out var t, out var isColl) &&
            isColl &&
            t?.Name == prop.ContainingType.Name);
    }

    private static string? FindInverseCollectionName(IPropertySymbol prop, INamedTypeSymbol targetType)
    {
        return targetType.GetMembers().OfType<IPropertySymbol>()
            .FirstOrDefault(p =>
                IsNavigationProperty(p, out var t, out var isColl) &&
                isColl &&
                t?.Name == prop.ContainingType.Name)
            ?.Name;
    }

    private static bool HasInverseReference(IPropertySymbol prop, INamedTypeSymbol targetType)
    {
        return targetType.GetMembers().OfType<IPropertySymbol>().Any(p =>
            IsNavigationProperty(p, out var t, out var isColl) &&
            !isColl &&
            t?.Name == prop.ContainingType.Name);
    }

    private INamedTypeSymbol? FindSymbolForEntity(EfEntity entity, Compilation compilation)
    {
        // Search in the same namespace as context first
        return GetNamedTypes(compilation.GlobalNamespace).FirstOrDefault(t => t.Name == entity.Name);
    }

    private static bool IsDbContext(ClassDeclarationSyntax @class)
    {
        return @class.BaseList?.Types.Any(t => t.ToString().Contains("DbContext")) ?? false;
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (type == null || baseType == null)
        {
            return false;
        }

        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType) || current.Name == "DbContext")
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private IEnumerable<INamedTypeSymbol> GetNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in GetNamedTypes(childNs))
            {
                yield return type;
            }
        }
    }
}

public static class SymbolExtensions
{
    public static bool IsNullable(this ITypeSymbol type)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated ||
               type.Name == "Nullable";
    }
}