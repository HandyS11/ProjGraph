using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Constants;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Extensions;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Patterns;

namespace ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;

/// <summary>
/// Infrastructure implementation for advanced Entity Framework model analysis using Roslyn and semantic models.
/// </summary>
public class EfModelAnalyzer(ICompilationFactory compilationFactory, IFileSystem fileSystem) : IEfModelAnalyzer
{
    /// <summary>
    /// Discovers all DbContext classes in the provided syntax tree.
    /// </summary>
    /// <param name="root">The root <see cref="SyntaxNode"/> to analyze.</param>
    /// <returns>A collection of DbContext class names found in the syntax tree.</returns>
    /// <seealso cref="ClassDeclarationSyntax"/>
    /// <seealso cref="DbContextIdentifier.IsDbContext(ClassDeclarationSyntax)"/>
    public IEnumerable<string> DiscoverDbContexts(SyntaxNode root)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(DbContextIdentifier.IsDbContext)
            .Select(c => c.Identifier.Text)
            .Distinct();
    }

    /// <summary>
    /// Discovers all ModelSnapshot classes in the provided syntax tree.
    /// </summary>
    /// <param name="root">The root <see cref="SyntaxNode"/> to analyze.</param>
    /// <returns>A collection of ModelSnapshot class names found in the syntax tree.</returns>
    /// <seealso cref="ClassDeclarationSyntax"/>
    /// <seealso cref="DbContextIdentifier.IsModelSnapshot(ClassDeclarationSyntax)"/>
    public IEnumerable<string> DiscoverModelSnapshots(SyntaxNode root)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(DbContextIdentifier.IsModelSnapshot)
            .Select(c => c.Identifier.Text)
            .Distinct();
    }

    /// <summary>
    /// Analyzes an Entity Framework ModelSnapshot file to extract entity and relationship information.
    /// </summary>
    /// <param name="snapshotPath">The file path to the ModelSnapshot class.</param>
    /// <param name="snapshotName">Optional name of the specific snapshot to analyze. If null, the first snapshot found is used.</param>
    /// <returns>A <see cref="Task{TResult}"/> that resolves to an <see cref="EfModel"/> containing entities and relationships.</returns>
    /// <exception cref="InvalidOperationException">Thrown when ModelSnapshot is not found in the file or semantic symbol cannot be resolved.</exception>
    /// <seealso cref="BuildSnapshotSyntaxTreesAsync(string, ClassDeclarationSyntax, string, SyntaxTree)"/>
    /// <seealso cref="ModelSnapshotParser.Parse(ClassDeclarationSyntax, INamedTypeSymbol, Compilation)"/>
    /// <seealso cref="RelationshipAnalyzer.AnalyzeRelationships(EfModel, Dictionary{string, EfEntity}, Compilation)"/>
    public async Task<EfModel> AnalyzeSnapshotAsync(string snapshotPath, string? snapshotName)
    {
        var code = fileSystem.ReadAllText(snapshotPath);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = await syntaxTree.GetRootAsync();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var snapshotClass = DbContextIdentifier.FindSnapshotClass(classDeclarations, snapshotName)
                            ?? throw new InvalidOperationException("ModelSnapshot not found in file");

        var snapshotDirectory = fileSystem.GetDirectoryName(snapshotPath) ?? Environment.CurrentDirectory;

        var syntaxTrees =
            await BuildSnapshotSyntaxTreesAsync(snapshotPath, snapshotClass, snapshotDirectory, syntaxTree);
        var compilation = compilationFactory.CreateCompilation(syntaxTrees);

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
    /// Analyzes an Entity Framework DbContext file to extract entity and relationship information.
    /// </summary>
    /// <param name="path">The file path to the DbContext class.</param>
    /// <param name="contextName">Optional name of the specific context to analyze. If null, the first context found is used.</param>
    /// <returns>A <see cref="Task{TResult}"/> that resolves to an <see cref="EfModel"/> containing entities and relationships.</returns>
    /// <exception cref="InvalidOperationException">Thrown when DbContext is not found in the file or semantic symbol cannot be resolved.</exception>
    /// <seealso cref="BuildSyntaxTreesAsync(string, ClassDeclarationSyntax, string, SyntaxTree)"/>
    /// <seealso cref="BuildEfModel(INamedTypeSymbol, Compilation)"/>
    /// <seealso cref="DbContextIdentifier.FindContextClass(IEnumerable{ClassDeclarationSyntax}, string?)"/>
    public async Task<EfModel> AnalyzeContextAsync(string path, string? contextName)
    {
        var code = fileSystem.ReadAllText(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = await syntaxTree.GetRootAsync();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var contextClass = DbContextIdentifier.FindContextClass(classDeclarations, contextName)
                           ?? throw new InvalidOperationException("DbContext not found in file");

        var contextDirectory = fileSystem.GetDirectoryName(path) ?? Environment.CurrentDirectory;

        var syntaxTrees = await BuildSyntaxTreesAsync(path, contextClass, contextDirectory, syntaxTree);
        var compilation = compilationFactory.CreateCompilation(syntaxTrees);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var contextType = semanticModel.GetDeclaredSymbol(contextClass)
                          ?? throw new InvalidOperationException("Could not get semantic symbol for context");

        return BuildEfModel(contextType, compilation);
    }

    /// <summary>
    /// Builds a collection of <see cref="SyntaxTree"/> objects for the DbContext and all its related entity files.
    /// </summary>
    /// <param name="contextPath">The file path to the DbContext class.</param>
    /// <param name="contextClass">The <see cref="ClassDeclarationSyntax"/> of the DbContext class.</param>
    /// <param name="contextDirectory">The directory containing the DbContext file.</param>
    /// <param name="contextSyntaxTree">The <see cref="SyntaxTree"/> of the DbContext file.</param>
    /// <returns>A <see cref="Task{TResult}"/> that resolves to a list of <see cref="SyntaxTree"/> objects.</returns>
    /// <seealso cref="EntityFileDiscovery.ExtractEntityTypeNames(ClassDeclarationSyntax)"/>
    /// <seealso cref="EntityFileDiscovery.DiscoverEntityFilesAsync(List{string}, HashSet{string}, string)"/>
    /// <seealso cref="EntityFileDiscovery.DiscoverBaseClassFilesAsync(Dictionary{string, string}, string)"/>
    /// <seealso cref="CreateSyntaxTreesAsync(SyntaxTree, Dictionary{string, string})"/>
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
    /// Builds an <see cref="EfModel"/> from a DbContext type symbol.
    /// </summary>
    /// <param name="contextType">The <see cref="INamedTypeSymbol"/> representing the DbContext class.</param>
    /// <param name="compilation">The <see cref="Compilation"/> containing semantic information.</param>
    /// <returns>An <see cref="EfModel"/> containing entities and relationships.</returns>
    /// <seealso cref="DiscoverEntitiesFromDbSets(INamedTypeSymbol)"/>
    /// <seealso cref="FluentApiConfigurationParser.ApplyFluentApiConstraints(INamedTypeSymbol, Dictionary{string, EfEntity}, EfModel, Compilation)"/>
    /// <seealso cref="RelationshipAnalyzer.AnalyzeRelationships(EfModel, Dictionary{string, EfEntity}, Compilation)"/>
    /// <seealso cref="DeduplicateModelContent(EfModel)"/>
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
    /// Removes duplicate entities and relationships from an <see cref="EfModel"/>.
    /// </summary>
    /// <param name="model">The <see cref="EfModel"/> to deduplicate.</param>
    /// <remarks>
    /// When duplicate relationships exist between the same entities, prefers <see cref="EfRelationshipType.OneToMany"/> relationships.
    /// </remarks>
    /// <seealso cref="EfEntity"/>
    /// <seealso cref="EfRelationship"/>
    private static void DeduplicateModelContent(EfModel model)
    {
        var uniqueEntities = model.Entities
            .GroupBy(e => e.Name)
            .Select(g => g.First())
            .ToList();
        model.Entities.Clear();
        model.Entities.AddRange(uniqueEntities);

        var uniqueRelationships = model.Relationships
            .GroupBy(r => r.GenerateKey())
            .Select(g => g.First())
            .ToList();

        var finalRelationships = uniqueRelationships
            .GroupBy(r => new { r.SourceEntity, r.TargetEntity })
            .Select(g =>
            {
                if (g.Count() == 1)
                {
                    return g.First();
                }

                var oneToMany = g.FirstOrDefault(r => r.Type == EfRelationshipType.OneToMany);
                return oneToMany ?? g.First();
            })
            .ToList();

        model.Relationships.Clear();
        model.Relationships.AddRange(finalRelationships);
    }

    /// <summary>
    /// Discovers and analyzes all entity types from DbSet properties in a DbContext.
    /// </summary>
    /// <param name="contextType">The <see cref="INamedTypeSymbol"/> representing the DbContext class.</param>
    /// <returns>A dictionary mapping entity names to their corresponding <see cref="EfEntity"/> objects.</returns>
    /// <seealso cref="IPropertySymbol"/>
    /// <seealso cref="INamedTypeSymbol"/>
    /// <seealso cref="EntityAnalyzer.AnalyzeEntity(INamedTypeSymbol)"/>
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

    /// <summary>
    /// Builds a collection of <see cref="SyntaxTree"/> objects for a ModelSnapshot and all its related entity files.
    /// </summary>
    /// <param name="snapshotPath">The file path to the ModelSnapshot class.</param>
    /// <param name="snapshotClass">The <see cref="ClassDeclarationSyntax"/> of the ModelSnapshot class.</param>
    /// <param name="snapshotDirectory">The directory containing the ModelSnapshot file.</param>
    /// <param name="snapshotSyntaxTree">The <see cref="SyntaxTree"/> of the ModelSnapshot file.</param>
    /// <returns>A <see cref="Task{TResult}"/> that resolves to a list of <see cref="SyntaxTree"/> objects.</returns>
    /// <seealso cref="ExtractEntityTypeNamesFromSnapshot(ClassDeclarationSyntax)"/>
    /// <seealso cref="EntityFileDiscovery.DiscoverEntityFilesAsync(List{string}, HashSet{string}, string)"/>
    /// <seealso cref="EntityFileDiscovery.DiscoverBaseClassFilesAsync(Dictionary{string, string}, string)"/>
    /// <seealso cref="CreateSyntaxTreesAsync(SyntaxTree, Dictionary{string, string})"/>
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
    /// Extracts entity type names from a ModelSnapshot class by parsing the BuildModel method.
    /// </summary>
    /// <param name="snapshotClass">The <see cref="ClassDeclarationSyntax"/> of the ModelSnapshot class.</param>
    /// <returns>A <see cref="HashSet{T}"/> containing the names of all entities found in the snapshot.</returns>
    /// <seealso cref="MethodDeclarationSyntax"/>
    /// <seealso cref="EfAnalysisConstants.EfMethods.BuildModel"/>
    /// <seealso cref="EfAnalysisRegexPatterns.EntityMatchRegex"/>
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
    /// Merges two dictionaries containing file paths, ensuring no duplicate keys exist.
    /// </summary>
    /// <param name="target">The target dictionary to merge into.</param>
    /// <param name="source">The source dictionary to merge from.</param>
    /// <remarks>
    /// If a key exists in both dictionaries, the value from the target dictionary is preserved.
    /// </remarks>
    private static void MergeFileDictionaries(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (var kvp in source.Where(kvp => !target.ContainsKey(kvp.Key)))
        {
            target[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Asynchronously creates a list of syntax trees from a given context syntax tree and a collection of entity files.
    /// </summary>
    /// <param name="contextTree">The syntax tree representing the context.</param>
    /// <param name="entityFiles">A dictionary containing entity file paths with their corresponding names as keys.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of syntax trees.</returns>
    private static async Task<List<SyntaxTree>> CreateSyntaxTreesAsync(SyntaxTree contextTree,
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
}