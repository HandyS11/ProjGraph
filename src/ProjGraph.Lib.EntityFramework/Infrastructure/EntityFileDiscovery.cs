using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Provides methods for discovering entity and base class files within a specified context directory
/// and related search directories.
/// </summary>
/// <remarks>
/// This class contains methods to facilitate the discovery of entity files and their base class files
/// in a project. It includes functionality for extracting entity type names, building search directories,
/// and processing source files to identify relevant entities and their relationships.
/// </remarks>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
internal sealed class EntityFileDiscovery(IFileSystem fileSystem) : IEntityFileDiscovery
{
    /// <summary>
    /// Maximum recursion depth when searching for base class files to prevent infinite recursion
    /// and limit search scope to reasonable project structures.
    /// </summary>
    private const int MaxSearchDepth = 10;

    /// <summary>
    /// Discovers the file paths of entity files within the specified search directories.
    /// </summary>
    /// <param name="searchDirectories">A list of directories to search for entity files.</param>
    /// <param name="entityTypeNames">A set of entity type names to search for in the files.</param>
    /// <param name="contextFilePath">The file path of the context file to exclude from the search.</param>
    /// <returns>
    /// A <see cref="Dictionary{TKey, TValue}"/> where the keys are entity type names and the values are the corresponding file paths.
    /// </returns>
    /// <remarks>
    /// This method iterates through the provided search directories and asynchronously searches for C# files
    /// containing entity type definitions. It excludes the context file specified by <paramref name="contextFilePath"/>.
    /// For each directory, it calls <see cref="SearchDirectoryForEntitiesAsync"/> to process the files and populate
    /// the resulting dictionary with matching entity type names and their file paths.
    /// </remarks>
    public async Task<Dictionary<string, string>> DiscoverEntityFilesAsync(
        IReadOnlyList<string> searchDirectories,
        HashSet<string> entityTypeNames,
        string contextFilePath)
    {
        var entityFiles = new Dictionary<string, string>();
        var normalizedContextPath = fileSystem.GetFullPath(contextFilePath);

        foreach (var searchDir in searchDirectories.Where(fileSystem.DirectoryExists))
        {
            await SearchDirectoryForEntitiesAsync(searchDir, entityTypeNames, normalizedContextPath, entityFiles);

            // Optimization: stop searching if we've found all entities
            if (entityTypeNames.All(entityFiles.ContainsKey))
            {
                break;
            }
        }

        return entityFiles;
    }

    /// <summary>
    /// Discovers source files declaring an <c>IEntityTypeConfiguration&lt;T&gt;</c> class within the given
    /// search directories, so config classes that live in their own files are added to the compilation. The
    /// context file is excluded (its declarations are already in the primary syntax tree).
    /// </summary>
    /// <param name="searchDirectories">The directories to search recursively.</param>
    /// <param name="contextFilePath">The DbContext file path to exclude.</param>
    /// <returns>A dictionary of config-class name to file path.</returns>
    public async Task<Dictionary<string, string>> DiscoverConfigurationFilesAsync(
        IReadOnlyList<string> searchDirectories,
        string contextFilePath)
    {
        var configFiles = new Dictionary<string, string>();
        var normalizedContextPath = fileSystem.GetFullPath(contextFilePath);

        foreach (var searchDir in searchDirectories.Where(fileSystem.DirectoryExists))
        {
            await SearchDirectoryForConfigurationsAsync(searchDir, normalizedContextPath, configFiles);
        }

        return configFiles;
    }

    /// <summary>
    /// Discovers the file paths of base class files for the given entity files within the specified context directory.
    /// </summary>
    /// <param name="entityFiles">
    /// A dictionary where the keys are entity type names and the values are the corresponding file paths.
    /// </param>
    /// <param name="contextDirectory">The directory containing the context file.</param>
    /// <returns>
    /// A <see cref="Dictionary{TKey, TValue}"/> where the keys are base class names and the values are the corresponding file paths.
    /// If no base class files are found, an empty dictionary is returned.
    /// </returns>
    /// <remarks>
    /// This method extracts the names of base classes from the provided entity files asynchronously.
    /// If no base class names are found, an empty dictionary is returned.
    /// Otherwise, it determines the solution root directory by traversing up the directory hierarchy
    /// from the context directory, and searches for the base class files within the solution root.
    /// </remarks>
    public async Task<Dictionary<string, string>> DiscoverBaseClassFilesAsync(
        Dictionary<string, string> entityFiles,
        string contextDirectory)
    {
        var baseClassNames = await ExtractBaseClassNamesAsync(entityFiles);
        if (baseClassNames.Count is 0)
        {
            return [];
        }

        // Search in context directory and its parents for base classes
        var solutionRoot = WorkspaceRootResolver.FindSolutionRoot(contextDirectory, 3);
        return SearchForBaseClassFiles(baseClassNames, solutionRoot);
    }

    /// <summary>
    /// Builds a list of directories to search for entity files based on the provided context directory.
    /// </summary>
    /// <param name="contextDirectory">The directory containing the context file.</param>
    /// <returns>
    /// A list of directories to search for entity files, including the context directory and its parent directory.
    /// </returns>
    /// <remarks>
    /// This method starts with the context directory and then its parent directory.
    /// Since the parent directory scan is recursive, it will naturally include the context directory
    /// and all siblings through the recursive search.
    /// </remarks>
    public IReadOnlyList<string> BuildSearchDirectories(
        string contextDirectory)
    {
        var searchDirectories = new List<string>
        {
            contextDirectory
        };
        var parentDir = Directory.GetParent(contextDirectory);

        if (parentDir is null)
        {
            return searchDirectories;
        }

        // Avoid searching outside the temp directory if we are in one,
        // to prevent finding files from parallel test runs.
        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (contextDirectory.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
        {
            return searchDirectories;
        }

        // Adding the parent directory handles most cases as it encompasses siblings and the context dir itself.
        searchDirectories.Add(parentDir.FullName);

        return [.. searchDirectories.Distinct()];
    }

    /// <summary>
    /// Extracts the names of entity types from the provided context class declaration.
    /// </summary>
    /// <param name="contextClass">The <see cref="ClassDeclarationSyntax"/> representing the context class.</param>
    /// <returns>
    /// A <see cref="HashSet{T}"/> containing the names of entity types defined in the context class.
    /// </returns>
    /// <remarks>
    /// This method iterates through the members of the context class, identifying properties of type <c>DbSet&lt;T&gt;</c>.
    /// It extracts the type argument <c>T</c> from each <c>DbSet&lt;T&gt;</c> property and adds it to the resulting set.
    /// </remarks>
    public HashSet<string> ExtractEntityTypeNames(ClassDeclarationSyntax contextClass)
    {
        return [.. contextClass.Members
            .OfType<PropertyDeclarationSyntax>()
            // Unwrap DbSet<T>? so the nullable-annotated form is recognized like the bare form.
            .Select(member => member.Type is NullableTypeSyntax nullable ? nullable.ElementType : member.Type)
            .OfType<GenericNameSyntax>()
            .Where(genericType => genericType is
            {
                Identifier.Text: EfAnalysisConstants.CommonNames.DbSet, TypeArgumentList.Arguments.Count: 1
            })
            // Reduce the type argument to its simple name so DbSet<Models.Blog> keys as "Blog".
            .Select(genericType => SimpleTypeName(genericType.TypeArgumentList.Arguments[0]))
        ];
    }

    /// <summary>Reduces a type-argument syntax to its simple identifier (last segment of a qualified name).</summary>
    /// <param name="type">The type-argument syntax from a <c>DbSet&lt;T&gt;</c> property.</param>
    /// <returns>The simple type name.</returns>
    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => type.ToString()
    };

    /// <summary>
    /// Searches a directory and its subdirectories for C# files containing entity type definitions
    /// and adds their file paths to the provided dictionary.
    /// </summary>
    /// <param name="searchDir">The directory to search for C# files.</param>
    /// <param name="entityTypeNames">A set of entity type names to search for in the C# files.</param>
    /// <param name="normalizedContextPath">The normalized file path of the context file to exclude from the search.</param>
    /// <param name="entityFiles">
    /// A dictionary where the keys are entity type names and the values are the corresponding file paths.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method iterates through all C# files in the specified directory and its subdirectories.
    /// It skips the context file specified by <paramref name="normalizedContextPath"/>.
    /// For each file, it calls <see cref="ProcessSourceFileAsync"/> to process the file and add matching
    /// entity type names and their file paths to the <paramref name="entityFiles"/> dictionary.
    /// Any access errors encountered during directory traversal are ignored.
    /// Directories like bin, obj, .git, and node_modules are skipped during traversal for performance.
    /// </remarks>
    private async Task SearchDirectoryForEntitiesAsync(
        string searchDir,
        HashSet<string> entityTypeNames,
        string normalizedContextPath,
        Dictionary<string, string> entityFiles)
    {
        await SearchDirectoryRecursiveAsync(searchDir, entityTypeNames, normalizedContextPath, entityFiles);
    }

    /// <summary>
    /// Recursively searches a directory for C# files, skipping common build and version control directories.
    /// </summary>
    /// <param name="currentDir">The current directory to search.</param>
    /// <param name="entityTypeNames">A set of entity type names to search for in the C# files.</param>
    /// <param name="normalizedContextPath">The normalized file path of the context file to exclude from the search.</param>
    /// <param name="entityFiles">
    /// A dictionary where the keys are entity type names and the values are the corresponding file paths.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method implements manual recursion to avoid descending into directories that typically
    /// contain build artifacts or dependencies (bin, obj, .git, node_modules), improving performance
    /// for large projects.
    /// </remarks>
    private async Task SearchDirectoryRecursiveAsync(
        string currentDir,
        HashSet<string> entityTypeNames,
        string normalizedContextPath,
        Dictionary<string, string> entityFiles)
    {
        try
        {
            // Process files in the current directory
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            };

            foreach (var csFile in fileSystem.EnumerateFiles(
                         currentDir,
                         EfAnalysisConstants.FilePatterns.CSharpFiles,
                         options))
            {
                var fullPath = fileSystem.GetFullPath(csFile);
                if (fullPath.Equals(normalizedContextPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await ProcessSourceFileAsync(fullPath, entityTypeNames, entityFiles);
            }

            // Recursively process subdirectories, skipping excluded directories
            foreach (var subDir in fileSystem.EnumerateDirectories(currentDir, "*", options))
            {
                if (DirectoryFilters.ShouldSkipDirectory(subDir))
                {
                    continue;
                }

                await SearchDirectoryRecursiveAsync(subDir, entityTypeNames, normalizedContextPath, entityFiles);
            }
        }
        catch (IOException)
        {
            // Ignore access errors for directories we can't read
        }
    }

    /// <summary>
    /// Processes a source file to identify class declarations that match the specified entity type names
    /// and adds their file paths to the provided dictionary.
    /// </summary>
    /// <param name="filePath">The path of the source file to process.</param>
    /// <param name="entityTypeNames">A set of entity type names to search for in the source file.</param>
    /// <param name="entityFiles">
    /// A dictionary where the keys are entity type names and the values are the corresponding file paths.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method reads the content of the specified source file, parses it into a syntax tree, and retrieves the root node.
    /// It then searches for type declarations that match the provided entity type names and adds their file paths
    /// to the dictionary if they are not already present. Matches <see cref="TypeDeclarationSyntax"/>, not just
    /// <see cref="ClassDeclarationSyntax"/>, so a <c>record</c>-declared entity or owned type (e.g. a
    /// <c>public record Money(decimal Amount, string Currency)</c> value object) is found too — <c>record</c>
    /// declarations parse as <see cref="RecordDeclarationSyntax"/>, which does not derive from
    /// <see cref="ClassDeclarationSyntax"/>.
    /// </remarks>
    private async Task ProcessSourceFileAsync(
        string filePath,
        HashSet<string> entityTypeNames,
        Dictionary<string, string> entityFiles)
    {
        var fileCode = await fileSystem.ReadAllTextAsync(filePath);

        // Performance optimization: skip heavy parsing if none of the entity names are present in the text
        if (!entityTypeNames.Any(name => fileCode.Contains(name, StringComparison.Ordinal)))
        {
            return;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(fileCode);
        var root = await syntaxTree.GetRootAsync();

        foreach (var typeDecl in root.DescendantNodes()
                     .OfType<TypeDeclarationSyntax>()
                     .Where(typeDecl => entityTypeNames.Contains(typeDecl.Identifier.Text)))
        {
            entityFiles.TryAdd(typeDecl.Identifier.Text, filePath);
        }
    }

    /// <summary>
    /// Recursively searches a directory for source files declaring an <c>IEntityTypeConfiguration&lt;T&gt;</c>
    /// class, skipping the context file and build-artifact directories.
    /// </summary>
    /// <param name="currentDir">The directory to search.</param>
    /// <param name="normalizedContextPath">The normalized context file path to exclude.</param>
    /// <param name="configFiles">The dictionary of config-class name to file path, augmented in place.</param>
    private async Task SearchDirectoryForConfigurationsAsync(
        string currentDir,
        string normalizedContextPath,
        Dictionary<string, string> configFiles)
    {
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            };

            foreach (var csFile in fileSystem.EnumerateFiles(
                         currentDir,
                         EfAnalysisConstants.FilePatterns.CSharpFiles,
                         options))
            {
                var fullPath = fileSystem.GetFullPath(csFile);
                if (fullPath.Equals(normalizedContextPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await ProcessConfigurationFileAsync(fullPath, configFiles);
            }

            foreach (var subDir in fileSystem.EnumerateDirectories(currentDir, "*", options))
            {
                if (DirectoryFilters.ShouldSkipDirectory(subDir))
                {
                    continue;
                }

                await SearchDirectoryForConfigurationsAsync(subDir, normalizedContextPath, configFiles);
            }
        }
        catch (IOException)
        {
            // Ignore access errors for directories we can't read
        }
    }

    /// <summary>
    /// Adds a source file to <paramref name="configFiles"/> for each <c>IEntityTypeConfiguration&lt;T&gt;</c>
    /// class it declares. Skips files whose text does not mention the interface (a cheap pre-parse guard).
    /// </summary>
    /// <param name="filePath">The path of the source file to inspect.</param>
    /// <param name="configFiles">The dictionary of config-class name to file path, augmented in place.</param>
    private async Task ProcessConfigurationFileAsync(string filePath, Dictionary<string, string> configFiles)
    {
        var fileCode = await fileSystem.ReadAllTextAsync(filePath);
        if (!fileCode.Contains(EfAnalysisConstants.EfMethods.EntityTypeConfigurationInterface, StringComparison.Ordinal))
        {
            return;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(fileCode);
        var root = await syntaxTree.GetRootAsync();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var implementsInterface = classDecl.BaseList?.Types
                .Select(baseType => baseType.Type)
                .OfType<GenericNameSyntax>()
                .Any(generic =>
                    generic.Identifier.Text == EfAnalysisConstants.EfMethods.EntityTypeConfigurationInterface
                    && generic.TypeArgumentList.Arguments.Count == 1) ?? false;

            if (implementsInterface)
            {
                configFiles.TryAdd(classDecl.Identifier.Text, filePath);
            }
        }
    }

    /// <summary>
    /// Extracts the names of base classes from the provided entity files asynchronously.
    /// </summary>
    /// <param name="entityFiles">
    /// A dictionary where the keys are entity type names and the values are the corresponding file paths.
    /// </param>
    /// <returns>
    /// A <see cref="HashSet{T}"/> containing the names of the base classes extracted from the entity files.
    /// </returns>
    /// <remarks>
    /// This method reads the content of each unique entity file, parses it into a syntax tree, and retrieves the root node.
    /// It then extracts the base class names from the syntax tree using the <see cref="ExtractBaseClassNamesFromSyntax"/> method.
    /// </remarks>
    private async Task<HashSet<string>> ExtractBaseClassNamesAsync(Dictionary<string, string> entityFiles)
    {
        var baseClassNames = new HashSet<string>();

        foreach (var entityFile in entityFiles.Values.Distinct())
        {
            var entityCode = await fileSystem.ReadAllTextAsync(entityFile);
            var entityTree = CSharpSyntaxTree.ParseText(entityCode);
            var entityRoot = await entityTree.GetRootAsync();

            ExtractBaseClassNamesFromSyntax(entityRoot, baseClassNames);
        }

        return baseClassNames;
    }

    /// <summary>
    /// Extracts the names of base classes from the syntax tree of a given root node and adds them to the provided set.
    /// </summary>
    /// <param name="root">The root <see cref="SyntaxNode"/> of the syntax tree to analyze.</param>
    /// <param name="baseClassNames">A <see cref="HashSet{T}"/> to store the extracted base class names.</param>
    /// <remarks>
    /// This method traverses the syntax tree to find all class declarations with a base list.
    /// It then extracts the names of the base types using the <see cref="ExtractBaseTypeName"/> method
    /// and filters them using the <see cref="IsValidBaseClassName"/> method before adding them to the set.
    /// </remarks>
    public void ExtractBaseClassNamesFromSyntax(SyntaxNode root, HashSet<string> baseClassNames)
    {
        foreach (var baseTypeName in root.DescendantNodes()
                     .OfType<ClassDeclarationSyntax>()
                     .Where(classDecl => classDecl.BaseList is not null)
                     .SelectMany(classDecl => classDecl.BaseList!.Types)
                     .Select(ExtractBaseTypeName)
                     .Where(IsValidBaseClassName))
        {
            baseClassNames.Add(baseTypeName);
        }
    }

    /// <summary>
    /// Extracts the base type name from the provided <see cref="BaseTypeSyntax"/> object.
    /// </summary>
    /// <param name="baseType">The <see cref="BaseTypeSyntax"/> object representing the base type.</param>
    /// <returns>
    /// A string representing the name of the base type. If the base type includes generic parameters,
    /// the generic part is removed from the name.
    /// </returns>
    private static string ExtractBaseTypeName(BaseTypeSyntax baseType)
    {
        var baseTypeName = baseType.Type.ToString();
        if (baseTypeName.Contains('<', StringComparison.Ordinal))
        {
            baseTypeName = baseTypeName[..baseTypeName.IndexOf('<', StringComparison.Ordinal)];
        }

        return baseTypeName;
    }

    /// <summary>
    /// Determines whether the specified base class name is valid.
    /// </summary>
    /// <param name="baseTypeName">The name of the base class to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the base class name is valid (i.e., it is not an interface and is not "DbContext");
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// A valid base class name is one that does not start with 'I' followed by an uppercase letter (indicating an interface),
    /// and is not equal to "DbContext" (case-insensitive).
    /// </remarks>
    private static bool IsValidBaseClassName(string baseTypeName)
    {
        // Skip interfaces (start with 'I' followed by uppercase)
        var isInterface = baseTypeName.StartsWith('I') &&
                          baseTypeName.Length >= 2 &&
                          char.IsUpper(baseTypeName[1]);

        return !isInterface && !baseTypeName.Equals("DbContext", StringComparison.Ordinal);
    }

    /// <summary>
    /// Searches for base class files within the specified solution root directory based on the provided base class names.
    /// </summary>
    /// <param name="baseClassNames">A set of base class names to search for.</param>
    /// <param name="searchDirectory">The directory to search within.</param>
    /// <returns>
    /// A dictionary where the keys are base class names and the values are the corresponding file paths
    /// if the files are found; otherwise, the dictionary will be empty.
    /// </returns>
    /// <remarks>
    /// This method recursively searches for base class files while skipping common build and version control
    /// directories (bin, obj, .git, node_modules) to improve performance for large projects.
    /// </remarks>
    public Dictionary<string, string> SearchForBaseClassFiles(
        HashSet<string> baseClassNames,
        DirectoryInfo searchDirectory)
    {
        var baseClassFiles = new Dictionary<string, string>();
        SearchForBaseClassFilesRecursive(searchDirectory.FullName, baseClassNames, baseClassFiles, 0, MaxSearchDepth);
        return baseClassFiles;
    }

    /// <summary>
    /// Recursively searches for base class files, skipping common build and version control directories.
    /// </summary>
    /// <param name="currentDir">The current directory to search.</param>
    /// <param name="baseClassNames">A set of base class names to search for.</param>
    /// <param name="baseClassFiles">
    /// A dictionary to store the found base class files where the keys are base class names
    /// and the values are the corresponding file paths.
    /// </param>
    /// <param name="currentDepth">The current recursion depth.</param>
    /// <param name="maxDepth">The maximum recursion depth to prevent infinite recursion.</param>
    /// <remarks>
    /// This method implements manual recursion to avoid descending into directories that typically
    /// contain build artifacts or dependencies (bin, obj, .git, node_modules), improving performance
    /// for large projects.
    /// </remarks>
    private void SearchForBaseClassFilesRecursive(
        string currentDir,
        HashSet<string> baseClassNames,
        Dictionary<string, string> baseClassFiles,
        int currentDepth,
        int maxDepth)
    {
        if (currentDepth >= maxDepth || baseClassFiles.Count == baseClassNames.Count)
        {
            return;
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            };

            // Process files in the current directory
            foreach (var file in fileSystem.EnumerateFiles(
                         currentDir,
                         EfAnalysisConstants.FilePatterns.CSharpFiles,
                         options))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!baseClassNames.Contains(fileName))
                {
                    continue;
                }

                var fullPath = fileSystem.GetFullPath(file);
                baseClassFiles.TryAdd(fileName, fullPath);

                if (baseClassFiles.Count == baseClassNames.Count)
                {
                    return;
                }
            }

            // Recursively process subdirectories, skipping excluded directories
            foreach (var subDir in fileSystem.EnumerateDirectories(currentDir, "*", options))
            {
                if (DirectoryFilters.ShouldSkipDirectory(subDir))
                {
                    continue;
                }

                SearchForBaseClassFilesRecursive(subDir, baseClassNames, baseClassFiles, currentDepth + 1, maxDepth);

                if (baseClassFiles.Count == baseClassNames.Count)
                {
                    return;
                }
            }
        }
        catch (IOException)
        {
            // Ignore access errors
        }
    }
}
