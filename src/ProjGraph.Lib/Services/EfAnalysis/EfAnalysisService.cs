using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Services.EfAnalysis.Constants;
using ProjGraph.Lib.Services.EfAnalysis.Extensions;
using ProjGraph.Lib.Services.EfAnalysis.Patterns;

namespace ProjGraph.Lib.Services.EfAnalysis;

/// <summary>
/// Service for analyzing Entity Framework DbContext classes and their models.
/// </summary>
public class EfAnalysisService : IEfAnalysisService
{
    /// <summary>
    /// Discovers all DbContext classes within a specified C# file and returns their names as a list of strings.
    /// </summary>
    /// <param name="path">The file path to the C# file to analyze.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a list of strings representing
    /// the names of the discovered DbContext classes.
    /// </returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Validates that the provided file path corresponds to a C# source file.
    /// 2. Reads the content of the specified C# file asynchronously and parses it into a syntax tree.
    /// 3. Retrieves the root node of the syntax tree.
    /// 4. Identifies all class declarations in the syntax tree that are DbContext classes using the
    ///    <see cref="DbContextIdentifier.IsDbContext"/> method.
    /// 5. Extracts and returns the distinct names of the discovered DbContext classes.
    /// </remarks>
    public async Task<List<string>> DiscoverContextsAsync(string path)
    {
        ValidateCsFilePath(path);

        var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(path));
        var root = await syntaxTree.GetRootAsync();

        return
        [
            .. root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(DbContextIdentifier.IsDbContext)
                .Select(c => c.Identifier.Text)
                .Distinct()
        ];
    }

    /// <summary>
    /// Analyzes a specified DbContext class within a C# file and returns its Entity Framework model representation.
    /// </summary>
    /// <param name="path">The file path to the C# file containing the DbContext class.</param>
    /// <param name="contextName">
    /// The optional name of the DbContext class to analyze. If null, the first DbContext class found in the file is analyzed.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an <see cref="EfModel"/> object
    /// representing the analyzed DbContext and its associated entities.
    /// </returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Validates that the provided file path corresponds to a C# source file.
    /// 2. Calls the <see cref="AnalyzeFileAsync"/> method to perform the actual analysis of the DbContext class
    ///    and its associated entities.
    /// </remarks>
    public async Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null)
    {
        ValidateCsFilePath(path);

        return await AnalyzeFileAsync(path, contextName);
    }

    /// <inheritdoc />
    public async Task<List<string>> DiscoverSnapshotsAsync(string path)
    {
        ValidateCsFilePath(path);

        var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(path));
        var root = await syntaxTree.GetRootAsync();

        return
        [
            .. root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(DbContextIdentifier.IsModelSnapshot)
                .Select(c => c.Identifier.Text)
                .Distinct()
        ];
    }

    /// <inheritdoc />
    public async Task<EfModel> AnalyzeSnapshotAsync(string path, string? snapshotName = null)
    {
        ValidateCsFilePath(path);

        var code = await File.ReadAllTextAsync(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = await syntaxTree.GetRootAsync();

        var snapshotClass = FindSnapshotClass(root, snapshotName);
        var snapshotDirectory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();

        var syntaxTrees = await BuildSnapshotSyntaxTreesAsync(path, snapshotClass, snapshotDirectory, syntaxTree);
        var compilation = CompilationFactory.CreateCompilation(syntaxTrees);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var snapshotType = semanticModel.GetDeclaredSymbol(snapshotClass)
                           ?? throw new InvalidOperationException("Could not get semantic symbol for snapshot");

        var model = ModelSnapshotParser.Parse(snapshotClass, snapshotType, compilation);

        // Analyze relationships using the semantic model now that we have all symbols
        var entities = model.Entities.ToDictionary(e => e.Name);
        RelationshipAnalyzer.AnalyzeRelationships(model, entities, compilation);

        return model;
    }

    /// <summary>
    /// Builds a list of syntax trees for a ModelSnapshot and its related entity files.
    /// </summary>
    private static async Task<List<SyntaxTree>> BuildSnapshotSyntaxTreesAsync(
        string snapshotPath,
        ClassDeclarationSyntax snapshotClass,
        string snapshotDirectory,
        SyntaxTree snapshotSyntaxTree)
    {
        var root = await snapshotSyntaxTree.GetRootAsync();
        var entityTypeNames = ExtractEntityTypeNamesFromSnapshot(snapshotClass);
        var searchDirectories = EntityFileDiscovery.BuildSearchDirectories(snapshotDirectory);

        var entityFiles = await EntityFileDiscovery.DiscoverEntityFilesAsync(
            searchDirectories,
            entityTypeNames,
            snapshotPath);

        // Also discover base classes
        var baseClassFiles = await EntityFileDiscovery.DiscoverBaseClassFilesAsync(entityFiles, snapshotDirectory);

        var additionalBaseClassNames = new HashSet<string>();
        EntityFileDiscovery.ExtractBaseClassNamesFromSyntax(root, additionalBaseClassNames);
        var additionalBaseFiles =
            EntityFileDiscovery.SearchForBaseClassFiles(additionalBaseClassNames, new DirectoryInfo(snapshotDirectory));
        MergeFileDictionaries(baseClassFiles, additionalBaseFiles);

        MergeFileDictionaries(entityFiles, baseClassFiles);

        return await CreateSyntaxTreesAsync(snapshotSyntaxTree, entityFiles);
    }

    /// <summary>
    /// Extracts entity type names from a ModelSnapshot class by searching for .Entity calls.
    /// </summary>
    private static HashSet<string> ExtractEntityTypeNamesFromSnapshot(ClassDeclarationSyntax snapshotClass)
    {
        var entityTypeNames = new HashSet<string>();
        var buildModelMethod = snapshotClass.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == EfAnalysisConstants.EfMethods.BuildModel);

        if (buildModelMethod?.Body == null)
        {
            return entityTypeNames;
        }

        var methodText = buildModelMethod.ToString();

        // Match .Entity<T> or .Entity("Namespace.T")
        var entityMatches = EfAnalysisRegexPatterns.EntityMatchRegex().Matches(methodText);

        var shortNames = entityMatches
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .Where(fullName => !string.IsNullOrEmpty(fullName))
            .Select(fullName => fullName.Contains('.') ? fullName.Split('.')[^1] : fullName);

        foreach (var shortName in shortNames)
        {
            entityTypeNames.Add(shortName);
        }

        return entityTypeNames;
    }

    /// <summary>
    /// Validates that the provided file path corresponds to a C# source file.
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <exception cref="ArgumentException">Thrown if the file path does not end with ".cs".</exception>
    /// <remarks>
    /// This method checks if the given file path ends with the ".cs" extension (case-insensitive).
    /// If the file path does not meet this condition, an <see cref="ArgumentException"/> is thrown.
    /// </remarks>
    private static void ValidateCsFilePath(string path)
    {
        if (!path.EndsWith(EfAnalysisConstants.FilePatterns.CSharpExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Only {EfAnalysisConstants.FilePatterns.CSharpExtension} files are supported",
                nameof(path));
        }
    }

    /// <summary>
    /// Analyzes a C# file to extract information about a specified DbContext class and its associated entities.
    /// </summary>
    /// <param name="path">The file path to the C# file containing the DbContext class.</param>
    /// <param name="contextName">
    /// The optional name of the DbContext class to analyze. If null, the first DbContext class found in the file is analyzed.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an <see cref="EfModel"/> object
    /// representing the analyzed DbContext and its associated entities.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified DbContext class cannot be found or analyzed.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Reads the content of the specified C# file asynchronously.
    /// 2. Parses the file content into a syntax tree and retrieves its root node.
    /// 3. Finds the DbContext class in the syntax tree, optionally filtering by the provided context name.
    /// 4. Determines the directory of the C# file.
    /// 5. Builds a list of syntax trees for the DbContext and its related entity files.
    /// 6. Creates a compilation object from the syntax trees.
    /// 7. Retrieves the semantic model for the syntax tree and resolves the DbContext type.
    /// 8. Builds and returns an <see cref="EfModel"/> object by analyzing the DbContext type and its entities.
    /// </remarks>
    private static async Task<EfModel> AnalyzeFileAsync(string path, string? contextName)
    {
        var code = await File.ReadAllTextAsync(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = await syntaxTree.GetRootAsync();

        var contextClass = FindContextClass(root, contextName);
        var contextDirectory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();

        var syntaxTrees = await BuildSyntaxTreesAsync(path, contextClass, contextDirectory, syntaxTree);
        var compilation = CompilationFactory.CreateCompilation(syntaxTrees);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var contextType = semanticModel.GetDeclaredSymbol(contextClass)
                          ?? throw new InvalidOperationException("Could not get semantic symbol for context");

        return BuildEfModel(contextType, compilation);
    }

    /// <summary>
    /// Finds the DbContext class within the given syntax tree root node, optionally filtering by a specific context name.
    /// </summary>
    /// <param name="root">The root syntax node of the syntax tree to search.</param>
    /// <param name="contextName">The optional name of the DbContext class to find. If null, the first DbContext class is returned.</param>
    /// <returns>The <see cref="ClassDeclarationSyntax"/> representing the DbContext class.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no DbContext class is found in the provided syntax tree.</exception>
    /// <remarks>
    /// This method retrieves all class declarations from the syntax tree root node and uses the 
    /// <see cref="DbContextIdentifier.FindContextClass"/> method to identify the DbContext class. 
    /// If a context name is provided, it will attempt to find a matching class; otherwise, it will return the first DbContext class found.
    /// </remarks>
    private static ClassDeclarationSyntax FindContextClass(SyntaxNode root, string? contextName)
    {
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        return DbContextIdentifier.FindContextClass(classDeclarations, contextName)
               ?? throw new InvalidOperationException("DbContext not found in file");
    }

    /// <summary>
    /// Finds the ModelSnapshot class within the given syntax tree root node, optionally filtering by a specific snapshot name.
    /// </summary>
    /// <param name="root">The root syntax node of the syntax tree to search.</param>
    /// <param name="snapshotName">The optional name of the ModelSnapshot class to find. If null, the first ModelSnapshot class is returned.</param>
    /// <returns>The <see cref="ClassDeclarationSyntax"/> representing the ModelSnapshot class.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no ModelSnapshot class is found in the provided syntax tree.</exception>
    private static ClassDeclarationSyntax FindSnapshotClass(SyntaxNode root, string? snapshotName)
    {
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        return DbContextIdentifier.FindSnapshotClass(classDeclarations, snapshotName)
               ?? throw new InvalidOperationException("ModelSnapshot not found in file");
    }

    /// <summary>
    /// Builds a list of syntax trees by analyzing the provided context syntax tree, context class, and related entity files.
    /// </summary>
    /// <param name="contextPath">The file path to the DbContext class.</param>
    /// <param name="contextClass">The <see cref="ClassDeclarationSyntax"/> representing the DbContext class.</param>
    /// <param name="contextDirectory">The directory containing the DbContext file.</param>
    /// <param name="contextSyntaxTree">The syntax tree representing the DbContext file.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="SyntaxTree"/> objects.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Extracts the root node from the provided context syntax tree.
    /// 2. Extracts the namespaces and entity type names from the context class.
    /// 3. Builds a list of search directories based on the context directory and extracted namespaces.
    /// 4. Discovers entity files in the search directories that match the extracted entity type names.
    /// 5. Discovers base class files for the identified entity files and merges them into the entity files dictionary.
    /// 6. Creates and returns a list of syntax trees by parsing the context syntax tree and the discovered entity files.
    /// </remarks>
    private static async Task<List<SyntaxTree>> BuildSyntaxTreesAsync(
        string contextPath,
        ClassDeclarationSyntax contextClass,
        string contextDirectory,
        SyntaxTree contextSyntaxTree)
    {
        var root = await contextSyntaxTree.GetRootAsync();
        var entityTypeNames = EntityFileDiscovery.ExtractEntityTypeNames(contextClass);
        var searchDirectories = EntityFileDiscovery.BuildSearchDirectories(contextDirectory);

        var entityFiles = await EntityFileDiscovery.DiscoverEntityFilesAsync(
            searchDirectories,
            entityTypeNames,
            contextPath);

        // Also discover base classes for entities that might be in the context file itself
        var baseClassFiles = await EntityFileDiscovery.DiscoverBaseClassFilesAsync(entityFiles, contextDirectory);

        // New step: extract base classes from the context file root as well
        var additionalBaseClassNames = new HashSet<string>();
        EntityFileDiscovery.ExtractBaseClassNamesFromSyntax(root, additionalBaseClassNames);
        var additionalBaseFiles =
            EntityFileDiscovery.SearchForBaseClassFiles(additionalBaseClassNames, new DirectoryInfo(contextDirectory));
        MergeFileDictionaries(baseClassFiles, additionalBaseFiles);

        MergeFileDictionaries(entityFiles, baseClassFiles);

        return await CreateSyntaxTreesAsync(contextSyntaxTree, entityFiles);
    }

    /// <summary>
    /// Merges the source dictionary into the target dictionary by adding key-value pairs from the source
    /// that do not already exist in the target.
    /// </summary>
    /// <param name="target">The target dictionary to which key-value pairs will be added.</param>
    /// <param name="source">The source dictionary containing key-value pairs to be merged into the target.</param>
    /// <remarks>
    /// This method iterates through the key-value pairs in the source dictionary and adds them to the target
    /// dictionary only if the key does not already exist in the target dictionary.
    /// </remarks>
    private static void MergeFileDictionaries(
        Dictionary<string, string> target,
        Dictionary<string, string> source)
    {
        foreach (var kvp in source.Where(kvp => !target.ContainsKey(kvp.Key)))
        {
            target[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Creates a list of syntax trees by parsing the provided context syntax tree and additional entity files.
    /// </summary>
    /// <param name="contextTree">The syntax tree representing the context file.</param>
    /// <param name="entityFiles">A dictionary where the keys are entity type names and the values are file paths to the corresponding entity files.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="SyntaxTree"/> objects.</returns>
    /// <remarks>
    /// This method initializes the list of syntax trees with the provided context syntax tree. It then iterates through
    /// the distinct file paths in the entity files dictionary, reads the content of each file asynchronously, parses it
    /// into a syntax tree, and adds it to the list of syntax trees. The resulting list of syntax trees is returned.
    /// </remarks>
    private static async Task<List<SyntaxTree>> CreateSyntaxTreesAsync(
        SyntaxTree contextTree,
        Dictionary<string, string> entityFiles)
    {
        var syntaxTrees = new List<SyntaxTree> { contextTree };

        foreach (var entityFile in entityFiles.Values.Distinct())
        {
            var entityCode = await File.ReadAllTextAsync(entityFile);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(entityCode));
        }

        return syntaxTrees;
    }

    /// <summary>
    /// Builds an Entity Framework model by analyzing the specified DbContext type and its associated entities.
    /// </summary>
    /// <param name="contextType">The <see cref="INamedTypeSymbol"/> representing the DbContext type to analyze.</param>
    /// <param name="compilation">The <see cref="Compilation"/> object used to analyze the DbContext and its entities.</param>
    /// <returns>An <see cref="EfModel"/> object containing the analyzed DbContext name, entities, and their relationships.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Creates a new <see cref="EfModel"/> instance and sets its ContextName property to the name of the provided DbContext type.
    /// 2. Discovers all DbSet entities defined in the DbContext using the <see cref="DiscoverEntitiesFromDbSets"/> method.
    /// 3. Adds the discovered entities to the model.
    /// 4. Applies Fluent API constraints to the model using the <see cref="FluentApiConfigurationParser"/>.
    /// 5. Analyzes relationships between entities using the <see cref="RelationshipAnalyzer"/>.
    /// </remarks>
    private static EfModel BuildEfModel(INamedTypeSymbol contextType, Compilation compilation)
    {
        var model = new EfModel { ContextName = contextType.Name };
        var entities = DiscoverEntitiesFromDbSets(contextType);

        model.Entities.AddRange(entities.Values);
        FluentApiConfigurationParser.ApplyFluentApiConstraints(contextType, entities, model, compilation);
        RelationshipAnalyzer.AnalyzeRelationships(model, entities, compilation);

        // Deduplicate entities and relationships (in case any were added multiple times)
        DeduplicateModelContent(model);

        return model;
    }

    /// <summary>
    /// Removes duplicate entities and relationships from the model.
    /// </summary>
    private static void DeduplicateModelContent(EfModel model)
    {
        // Deduplicate entities by name
        var uniqueEntities = model.Entities
            .GroupBy(e => e.Name)
            .Select(g => g.First())
            .ToList();
        model.Entities.Clear();
        model.Entities.AddRange(uniqueEntities);

        // Deduplicate relationships by generating unique keys
        var uniqueRelationships = model.Relationships
            .GroupBy(r => r.GenerateKey())
            .Select(g => g.First())
            .ToList();

        // Additional deduplication for self-referencing relationships
        // This handles cases like PermissionEntry -> PermissionEntry
        // appearing as both OneToOne and OneToMany
        var finalRelationships = uniqueRelationships
            .GroupBy(r => new { r.SourceEntity, r.TargetEntity })
            .Select(g =>
            {
                // If only one, return it
                if (g.Count() == 1)
                {
                    return g.First();
                }

                // If multiple with same source and target, prefer OneToMany over OneToOne
                // for self-referencing (parent-child hierarchies)
                var oneToMany = g.FirstOrDefault(r => r.Type == EfRelationshipType.OneToMany);
                return oneToMany ?? g.First();
            })
            .ToList();

        model.Relationships.Clear();
        model.Relationships.AddRange(finalRelationships);
    }

    /// <summary>
    /// Discovers and analyzes all DbSet entities defined in the specified DbContext type.
    /// </summary>
    /// <param name="contextType">The <see cref="INamedTypeSymbol"/> representing the DbContext type to analyze.</param>
    /// <returns>A dictionary where the keys are entity type names and the values are <see cref="EfEntity"/> objects representing the entities.</returns>
    /// <remarks>
    /// This method iterates through all members of the provided DbContext type, identifies properties of type DbSet,
    /// and analyzes the associated entity types. If an entity type is not already in the dictionary, it is added
    /// after being analyzed by the <see cref="EntityAnalyzer"/>.
    /// </remarks>
    private static Dictionary<string, EfEntity> DiscoverEntitiesFromDbSets(INamedTypeSymbol contextType)
    {
        var entities = new Dictionary<string, EfEntity>();

        foreach (var member in contextType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.Type is not INamedTypeSymbol
                {
                    Name: EfAnalysisConstants.CommonNames.DbSet, TypeArguments.Length: 1
                } dbSetType)
            {
                continue;
            }

            if (dbSetType.TypeArguments[0] is INamedTypeSymbol entityType &&
                !entities.ContainsKey(entityType.Name))
            {
                entities[entityType.Name] = EntityAnalyzer.AnalyzeEntity(entityType);
            }
        }

        return entities;
    }
}