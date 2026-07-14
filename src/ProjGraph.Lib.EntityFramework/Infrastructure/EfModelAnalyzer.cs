using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Infrastructure implementation for advanced Entity Framework model analysis using Roslyn and semantic models.
/// </summary>
/// <param name="compilationFactory">The factory for creating Roslyn compilations.</param>
/// <param name="fileSystem">The file system abstraction for reading source files.</param>
/// <param name="entityFileDiscovery">The service for discovering entity-related source files.</param>
public class EfModelAnalyzer(
    ICompilationFactory compilationFactory,
    IFileSystem fileSystem,
    IEntityFileDiscovery entityFileDiscovery) : IEfModelAnalyzer
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
    /// <exception cref="AnalysisException">Thrown when ModelSnapshot is not found in the file or semantic symbol cannot be resolved.</exception>
    /// <seealso cref="BuildSnapshotSyntaxTreesAsync(string, ClassDeclarationSyntax, string, SyntaxTree)"/>
    /// <seealso cref="ModelSnapshotParser.Parse(ClassDeclarationSyntax, INamedTypeSymbol, Compilation)"/>
    /// <seealso cref="RelationshipAnalyzer.AnalyzeRelationships(EfModel, Dictionary{string, EfEntity}, Compilation)"/>
    public async Task<EfModel> AnalyzeSnapshotAsync(string snapshotPath, string? snapshotName)
    {
        var code = await fileSystem.ReadAllTextAsync(snapshotPath);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = await syntaxTree.GetRootAsync();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var snapshotClass = DbContextIdentifier.FindSnapshotClass(classDeclarations, snapshotName)
                            ?? throw new AnalysisException("ModelSnapshot not found in file");

        var snapshotDirectory = ResolveDirectory(snapshotPath);

        var syntaxTrees =
            await BuildSnapshotSyntaxTreesAsync(snapshotPath, snapshotClass, snapshotDirectory, syntaxTree);
        var compilation = compilationFactory.CreateCompilation(syntaxTrees);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var snapshotType = semanticModel.GetDeclaredSymbol(snapshotClass)
                           ?? throw new AnalysisException("Could not get semantic symbol for snapshot");

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
    /// <exception cref="AnalysisException">Thrown when DbContext is not found in the file or semantic symbol cannot be resolved.</exception>
    /// <seealso cref="BuildSyntaxTreesAsync(string, ClassDeclarationSyntax, string, SyntaxTree)"/>
    /// <seealso cref="BuildEfModel(INamedTypeSymbol, Compilation)"/>
    /// <seealso cref="DbContextIdentifier.FindContextClass(IEnumerable{ClassDeclarationSyntax}, string?)"/>
    public async Task<EfModel> AnalyzeContextAsync(string path, string? contextName)
    {
        var code = await fileSystem.ReadAllTextAsync(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = await syntaxTree.GetRootAsync();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var contextClass = DbContextIdentifier.FindContextClass(classDeclarations, contextName)
                           ?? throw new AnalysisException("DbContext not found in file");

        var contextDirectory = ResolveDirectory(path);

        var syntaxTrees = await BuildSyntaxTreesAsync(path, contextClass, contextDirectory, syntaxTree);
        var compilation = compilationFactory.CreateCompilation(syntaxTrees);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var contextType = semanticModel.GetDeclaredSymbol(contextClass)
                          ?? throw new AnalysisException("Could not get semantic symbol for context");

        return BuildEfModel(contextType, compilation);
    }

    /// <summary>
    /// Resolves the directory that contains the given file. <see cref="IFileSystem.GetDirectoryName"/>
    /// can return null or an empty string for a bare filename with no directory component, which
    /// would crash <c>new DirectoryInfo("")</c> / <c>Directory.GetParent("")</c> downstream; fall
    /// back to the current directory in that case.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The containing directory, or the current directory when the path has no directory component.</returns>
    private string ResolveDirectory(string path)
    {
        var directory = fileSystem.GetDirectoryName(path);
        return string.IsNullOrEmpty(directory) ? fileSystem.GetCurrentDirectory() : directory;
    }

    /// <summary>
    /// Builds a collection of <see cref="SyntaxTree"/> objects for the DbContext and all its related entity files.
    /// </summary>
    /// <param name="contextPath">The file path to the DbContext class.</param>
    /// <param name="contextClass">The <see cref="ClassDeclarationSyntax"/> of the DbContext class.</param>
    /// <param name="contextDirectory">The directory containing the DbContext file.</param>
    /// <param name="contextSyntaxTree">The <see cref="SyntaxTree"/> of the DbContext file.</param>
    /// <returns>A <see cref="Task{TResult}"/> that resolves to a list of <see cref="SyntaxTree"/> objects.</returns>
    /// <seealso cref="entityFileDiscovery.ExtractEntityTypeNames(ClassDeclarationSyntax)"/>
    /// <seealso cref="entityFileDiscovery.DiscoverEntityFilesAsync(IReadOnlyList{string}, HashSet{string}, string)"/>
    /// <seealso cref="entityFileDiscovery.DiscoverBaseClassFilesAsync(Dictionary{string, string}, string)"/>
    /// <seealso cref="CreateSyntaxTrees(SyntaxTree, Dictionary{string, string})"/>
    private async Task<List<SyntaxTree>> BuildSyntaxTreesAsync(
        string contextPath,
        ClassDeclarationSyntax contextClass,
        string contextDirectory,
        SyntaxTree contextSyntaxTree)
    {
        var root = await contextSyntaxTree.GetRootAsync();
        var entityTypeNames = entityFileDiscovery.ExtractEntityTypeNames(contextClass);
        var searchDirectories = entityFileDiscovery.BuildSearchDirectories(contextDirectory);

        var entityFiles = await entityFileDiscovery.DiscoverEntityFilesAsync(
            searchDirectories,
            entityTypeNames,
            contextPath);

        // Also discover base classes for entities that might be in the context file itself
        var baseClassFiles = await entityFileDiscovery.DiscoverBaseClassFilesAsync(entityFiles, contextDirectory);

        // New step: extract base classes from the context file root as well, following the
        // inheritance chain across files — a base context may itself derive from another
        // context declared in yet another file.
        var additionalBaseFiles = await DiscoverTransitiveBaseFilesAsync(root, contextDirectory);
        MergeFileDictionaries(baseClassFiles, additionalBaseFiles);

        MergeFileDictionaries(entityFiles, baseClassFiles);

        // Slice 5: DbSet<T> declared on a base context names entities the entry context never
        // mentions. Pull those entity type names from the discovered base-context files and
        // discover their files too, so base-declared entities in their own file materialize with
        // columns instead of resolving to a bare (member-less) type.
        var baseContextEntityNames = await ExtractEntityTypeNamesFromFilesAsync(additionalBaseFiles.Values);
        if (baseContextEntityNames.Count > 0)
        {
            var baseEntityFiles = await entityFileDiscovery.DiscoverEntityFilesAsync(
                searchDirectories,
                baseContextEntityNames,
                contextPath);
            MergeFileDictionaries(entityFiles, baseEntityFiles);
        }

        // Slice 4: pull separate IEntityTypeConfiguration<T> files into the compilation so config classes
        // that live in their own files are visible to EntityConfigurationWalker.
        var configFiles = await entityFileDiscovery.DiscoverConfigurationFilesAsync(searchDirectories, contextPath);
        MergeFileDictionaries(entityFiles, configFiles);

        return CreateSyntaxTrees(contextSyntaxTree, entityFiles);
    }

    /// <summary>
    /// Discovers base-class files reachable from the context file's syntax root, following the
    /// inheritance chain transitively: base names found in each discovered file are searched in
    /// turn, so a multi-file DbContext hierarchy (context → base → grand-base, each in its own
    /// file) is pulled into the compilation in full.
    /// </summary>
    /// <param name="root">The syntax root of the context file.</param>
    /// <param name="contextDirectory">The directory containing the context file, used as the search root.</param>
    /// <returns>A dictionary mapping discovered base-class names to their file paths.</returns>
    /// <seealso cref="entityFileDiscovery.ExtractBaseClassNamesFromSyntax(SyntaxNode, HashSet{string})"/>
    /// <seealso cref="entityFileDiscovery.SearchForBaseClassFiles(HashSet{string}, DirectoryInfo)"/>
    private async Task<Dictionary<string, string>> DiscoverTransitiveBaseFilesAsync(
        SyntaxNode root,
        string contextDirectory)
    {
        var searchRoot = new DirectoryInfo(contextDirectory);
        var discoveredFiles = new Dictionary<string, string>();

        // seenNames makes the walk cycle-safe; each iteration must discover a new file to continue.
        var seenNames = new HashSet<string>();
        entityFileDiscovery.ExtractBaseClassNamesFromSyntax(root, seenNames);
        var pendingNames = new HashSet<string>(seenNames);

        while (pendingNames.Count > 0)
        {
            var foundFiles = entityFileDiscovery.SearchForBaseClassFiles(pendingNames, searchRoot);
            pendingNames = [];

            foreach (var (baseName, filePath) in foundFiles)
            {
                if (!discoveredFiles.TryAdd(baseName, filePath))
                {
                    continue;
                }

                var fileRoot = await TryParseFileAsync(filePath);
                if (fileRoot is null)
                {
                    continue;
                }

                var nestedBaseNames = new HashSet<string>();
                entityFileDiscovery.ExtractBaseClassNamesFromSyntax(fileRoot, nestedBaseNames);
                pendingNames.UnionWith(nestedBaseNames.Where(seenNames.Add));
            }
        }

        return discoveredFiles;
    }

    /// <summary>
    /// Extracts the DbSet entity-type names declared across the class declarations of the given files.
    /// </summary>
    /// <param name="filePaths">The C# files to scan (typically the discovered base-context files).</param>
    /// <returns>The set of entity type names referenced by <c>DbSet&lt;T&gt;</c> properties in those files.</returns>
    /// <seealso cref="entityFileDiscovery.ExtractEntityTypeNames(ClassDeclarationSyntax)"/>
    private async Task<HashSet<string>> ExtractEntityTypeNamesFromFilesAsync(IEnumerable<string> filePaths)
    {
        var entityTypeNames = new HashSet<string>();

        foreach (var filePath in filePaths.Distinct())
        {
            var fileRoot = await TryParseFileAsync(filePath);
            if (fileRoot is null)
            {
                continue;
            }

            foreach (var classDeclaration in fileRoot.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                entityTypeNames.UnionWith(entityFileDiscovery.ExtractEntityTypeNames(classDeclaration));
            }
        }

        return entityTypeNames;
    }

    /// <summary>
    /// Reads and parses a C# file, returning its syntax root, or <c>null</c> when the file cannot
    /// be read (locked, deleted, inaccessible). Discovery degrades to whatever the compilation
    /// already contains instead of failing the whole analysis on one unreadable file.
    /// </summary>
    /// <param name="filePath">The C# file to read and parse.</param>
    /// <returns>The parsed syntax root, or <c>null</c> when the file is unreadable.</returns>
    private async Task<SyntaxNode?> TryParseFileAsync(string filePath)
    {
        try
        {
            var code = await fileSystem.ReadAllTextAsync(filePath);
            return await CSharpSyntaxTree.ParseText(code).GetRootAsync();
        }
        catch (IOException)
        {
            return null;
        }
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

        foreach (var entity in entities.Values)
        {
            model.Entities.Add(entity);
        }

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
        foreach (var entity in uniqueEntities)
        {
            model.Entities.Add(entity);
        }

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
        foreach (var relationship in finalRelationships)
        {
            model.Relationships.Add(relationship);
        }
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

        // Walk the context's base-type chain (derived -> base) so DbSet<T> members declared on a
        // base DbContext are discovered too, mirroring how EntityAnalyzer.AnalyzeEntity climbs the
        // entity hierarchy. Stop at System.Object; the EF DbContext base exposes no DbSet<T>.
        for (var currentType = contextType;
             currentType is not null && currentType.SpecialType is not SpecialType.System_Object;
             currentType = currentType.BaseType)
        {
            foreach (var member in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.Type is not INamedTypeSymbol
                    {
                        Name: EfAnalysisConstants.CommonNames.DbSet, TypeArguments.Length: 1
                    } dbSetType)
                {
                    continue;
                }

                // First-wins over the derived -> base walk: a derived re-declaration of a DbSet<T>
                // takes precedence over the base's, consistent with C# member hiding/overriding.
                if (dbSetType.TypeArguments[0] is INamedTypeSymbol entityType &&
                    !entities.ContainsKey(entityType.Name))
                {
                    entities[entityType.Name] = EntityAnalyzer.AnalyzeEntity(entityType);
                }
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
    /// <seealso cref="entityFileDiscovery.DiscoverEntityFilesAsync(IReadOnlyList{string}, HashSet{string}, string)"/>
    /// <seealso cref="entityFileDiscovery.DiscoverBaseClassFilesAsync(Dictionary{string, string}, string)"/>
    /// <seealso cref="CreateSyntaxTrees(SyntaxTree, Dictionary{string, string})"/>
    private async Task<List<SyntaxTree>> BuildSnapshotSyntaxTreesAsync(
        string snapshotPath,
        ClassDeclarationSyntax snapshotClass,
        string snapshotDirectory,
        SyntaxTree snapshotSyntaxTree)
    {
        var root = await snapshotSyntaxTree.GetRootAsync();
        var entityTypeNames = ExtractEntityTypeNamesFromSnapshot(snapshotClass);
        var searchDirectories = entityFileDiscovery.BuildSearchDirectories(snapshotDirectory);

        var entityFiles = await entityFileDiscovery.DiscoverEntityFilesAsync(
            searchDirectories,
            entityTypeNames,
            snapshotPath);

        // Also discover base classes
        var baseClassFiles = await entityFileDiscovery.DiscoverBaseClassFilesAsync(entityFiles, snapshotDirectory);

        var additionalBaseClassNames = new HashSet<string>();
        entityFileDiscovery.ExtractBaseClassNamesFromSyntax(root, additionalBaseClassNames);
        var additionalBaseFiles =
            entityFileDiscovery.SearchForBaseClassFiles(additionalBaseClassNames, new DirectoryInfo(snapshotDirectory));
        MergeFileDictionaries(baseClassFiles, additionalBaseFiles);

        MergeFileDictionaries(entityFiles, baseClassFiles);

        return CreateSyntaxTrees(snapshotSyntaxTree, entityFiles);
    }

    /// <summary>
    /// Extracts entity type names from a ModelSnapshot class by walking the syntax of its BuildModel
    /// method for <c>Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c> invocations.
    /// </summary>
    /// <param name="snapshotClass">The <see cref="ClassDeclarationSyntax"/> of the ModelSnapshot class.</param>
    /// <returns>A <see cref="HashSet{T}"/> containing the names of all entities found in the snapshot.</returns>
    /// <seealso cref="FluentEntityWalker.CollectEntityNames(MethodDeclarationSyntax)"/>
    private static HashSet<string> ExtractEntityTypeNamesFromSnapshot(ClassDeclarationSyntax snapshotClass)
    {
        var buildModelMethod = snapshotClass.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == EfAnalysisConstants.EfMethods.BuildModel);

        if (buildModelMethod is null || (buildModelMethod.Body is null && buildModelMethod.ExpressionBody is null))
        {
            return [];
        }

        return FluentEntityWalker.CollectEntityNames(buildModelMethod);
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
    /// Creates a list of syntax trees from a given context syntax tree and a collection of entity files.
    /// Files that cannot be read (locked, deleted, inaccessible) are skipped so the analysis degrades
    /// to the remaining sources instead of failing outright.
    /// </summary>
    /// <param name="contextTree">The syntax tree representing the context.</param>
    /// <param name="entityFiles">A dictionary containing entity file paths with their corresponding names as keys.</param>
    /// <returns>A list of syntax trees.</returns>
    private List<SyntaxTree> CreateSyntaxTrees(SyntaxTree contextTree,
        Dictionary<string, string> entityFiles)
    {
        var syntaxTrees = new List<SyntaxTree> { contextTree };

        foreach (var filePath in entityFiles.Values.Distinct())
        {
            try
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileSystem.ReadAllText(filePath)));
            }
            catch (IOException)
            {
                // Skip unreadable files; the compilation degrades to what it already contains.
            }
        }

        return syntaxTrees;
    }
}
