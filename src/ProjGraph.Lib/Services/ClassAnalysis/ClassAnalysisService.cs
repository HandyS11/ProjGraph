using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Services.EfAnalysis;
using ModelTypeKind = ProjGraph.Core.Models.TypeKind;
using TypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace ProjGraph.Lib.Services.ClassAnalysis;

/// <summary>
/// Represents a service for analyzing C# classes and their relationships.
/// This service implements the <see cref="IClassAnalysisService"/> interface
/// and provides methods to analyze C# source files and extract class definitions,
/// relationships, and other metadata.
/// </summary>
public class ClassAnalysisService : IClassAnalysisService
{
    /// <summary>
    /// Analyzes a C# source file to extract class definitions and their relationships.
    /// This method reads the file, parses its syntax tree, and performs an analysis to identify
    /// class models and their relationships based on the provided options.
    /// </summary>
    /// <param name="filePath">The path to the C# source file to analyze.</param>
    /// <param name="includeInheritance">
    /// A boolean value indicating whether to include inheritance relationships in the analysis.
    /// </param>
    /// <param name="includeDependencies">
    /// A boolean value indicating whether to include dependency relationships in the analysis.
    /// </param>
    /// <param name="maxDepth">The maximum depth of type relationships to analyze.</param>
    /// <returns>
    /// A <see cref="ClassModel"/> object containing the analyzed class definitions and their relationships.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public async Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        bool includeInheritance = true,
        bool includeDependencies = false,
        int maxDepth = 1)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Source file not found", filePath);
        }

        var startDir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var code = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);

        var compilation = CompilationFactory.CreateCompilation([syntaxTree]);

        var context = new AnalysisContext
        {
            AnalyzedTypeFullNames = [],
            Types = [],
            Relationships = [],
            Compilation = compilation,
            StartDirectory = startDir
        };

        var options = new AnalysisOptions
        {
            MaxDepth = maxDepth, IncludeInheritance = includeInheritance, IncludeDependencies = includeDependencies
        };

        var typesToAnalyze = new Queue<(INamedTypeSymbol Symbol, int Depth)>();

        await EnqueueInitialTypesAsync(syntaxTree, compilation, typesToAnalyze);

        await ProcessTypeQueueAsync(typesToAnalyze, context, options);

        return new ClassModel(Path.GetFileName(filePath), context.Types, context.Relationships);
    }

    /// <summary>
    /// Enqueues the initial types from the provided syntax tree into the analysis queue.
    /// This method extracts all type declarations from the syntax tree, resolves their symbols,
    /// and adds them to the queue for further analysis with an initial depth of 0.
    /// </summary>
    /// <param name="syntaxTree">The <see cref="SyntaxTree"/> representing the source code to analyze.</param>
    /// <param name="compilation">The <see cref="CSharpCompilation"/> used to obtain semantic information.</param>
    /// <param name="typesToAnalyze">
    /// A queue of tuples where each tuple contains a symbol representing a type and its depth in the analysis hierarchy.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task EnqueueInitialTypesAsync(
        SyntaxTree syntaxTree,
        CSharpCompilation compilation,
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze)
    {
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var typeDeclarations = (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<BaseTypeDeclarationSyntax>();

        foreach (var typeDecl in typeDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
            if (symbol is { } namedSymbol)
            {
                typesToAnalyze.Enqueue((namedSymbol, 0));
            }
        }
    }

    /// <summary>
    /// Processes a queue of types to analyze, extracting type definitions and their relationships,
    /// and updating the analysis context with the results.
    /// </summary>
    /// <param name="typesToAnalyze">
    /// A queue of tuples where each tuple contains a symbol representing a type and its depth in the analysis hierarchy.
    /// </param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <param name="options">The <see cref="AnalysisOptions"/> specifying the configuration for the analysis process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ProcessTypeQueueAsync(
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context,
        AnalysisOptions options)
    {
        while (typesToAnalyze.Count > 0)
        {
            var (symbol, depth) = typesToAnalyze.Dequeue();
            var fullName = GetFullyQualifiedName(symbol);

            if (!context.AnalyzedTypeFullNames.Add(fullName))
            {
                continue;
            }

            var typeDef = AnalyzeType(symbol);
            context.Types.Add(typeDef);

            if (depth >= options.MaxDepth)
            {
                continue;
            }

            var relatedSymbols = DiscoverRelatedTypes(symbol, options.IncludeInheritance, options.IncludeDependencies);

            await ProcessRelatedTypesAsync(
                relatedSymbols,
                fullName,
                depth,
                typesToAnalyze,
                context);
        }
    }

    /// <summary>
    /// Discovers related types for a given symbol based on the specified analysis options.
    /// This method identifies related types through inheritance and dependency relationships
    /// and returns a list of these relationships.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze for related types.</param>
    /// <param name="includeInheritance">
    /// A boolean value indicating whether to include inheritance relationships in the analysis.
    /// </param>
    /// <param name="includeDependencies">
    /// A boolean value indicating whether to include dependency relationships in the analysis.
    /// </param>
    /// <returns>
    /// A list of tuples where each tuple contains a related symbol and its corresponding relationship kind.
    /// </returns>
    private static List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>
        DiscoverRelatedTypes(
            INamedTypeSymbol symbol,
            bool includeInheritance,
            bool includeDependencies)
    {
        var relatedSymbols =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        if (includeInheritance)
        {
            AddInheritanceRelationships(symbol, relatedSymbols);
        }

        if (includeDependencies)
        {
            AddDependencyRelationships(symbol, relatedSymbols);
        }

        return relatedSymbols;
    }

    /// <summary>
    /// Adds inheritance relationships for a given symbol to the list of related symbols.
    /// This method identifies the base type and implemented interfaces of the provided symbol
    /// and adds them as inheritance or realization relationships, respectively.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze for inheritance relationships.</param>
    /// <param name="relatedSymbols">
    /// A list of tuples where each tuple contains a related symbol and its corresponding relationship kind.
    /// </param>
    private static void AddInheritanceRelationships(
        INamedTypeSymbol symbol,
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols)
    {
        if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            relatedSymbols.Add((symbol.BaseType, RelationshipKind.Inheritance, null, null));
        }

        relatedSymbols.AddRange(symbol.Interfaces.Select(iface =>
            (iface, RelationshipKind.Realization, (string?)null, (string?)null)));
    }

    /// <summary>
    /// Adds dependency relationships for a given symbol to the list of related symbols.
    /// This method identifies dependencies based on the properties, fields, method return types,
    /// and method parameter types of the provided symbol.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze for dependencies.</param>
    /// <param name="relatedSymbols">
    /// A list of tuples where each tuple contains a related symbol and its corresponding relationship kind.
    /// </param>
    private static void AddDependencyRelationships(
        INamedTypeSymbol symbol,
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols)
    {
        // Track unique type+label combinations to avoid exact duplicates
        var seenCombinations = new HashSet<(string TypeName, string? Label)>();

        // Also track types separately for method dependencies (which don't have labels)
        var seenMethodTypes = new HashSet<string>();

        // Process properties and fields for association relationships (has-a relationships)
        var propertySymbols = symbol.GetMembers().OfType<IPropertySymbol>().ToList();
        var fieldSymbols = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared) // Filter out compiler-generated backing fields
            .ToList();

        foreach (var prop in propertySymbols)
        {
            ProcessMemberType(prop.Type, prop.Name, relatedSymbols, seenCombinations);
        }

        foreach (var field in fieldSymbols)
        {
            ProcessMemberType(field.Type, field.Name, relatedSymbols, seenCombinations);
        }

        // Process method return types and parameters as dependencies
        var methodReturnTypes = symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .Select(m => m.ReturnType);

        var methodParamTypes = symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .SelectMany(m => m.Parameters.Select(p => p.Type));

        foreach (var type in methodReturnTypes.Concat(methodParamTypes))
        {
            if (type is not INamedTypeSymbol { SpecialType: SpecialType.None } namedType)
            {
                continue;
            }

            var extractedTypes = ExtractTypesFromGeneric(namedType);
            relatedSymbols.AddRange(from extracted in extractedTypes
                let typeName = extracted.Name
                where seenMethodTypes.Add(typeName) && !IsSystemType(extracted)
                select ((INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality))(extracted,
                    RelationshipKind.Dependency, null, null));
        }
    }

    /// <summary>
    /// Processes a member type (property or field) to determine the appropriate relationship kind.
    /// </summary>
    /// <param name="type">The type of the member.</param>
    /// <param name="memberName">The name of the property or field.</param>
    /// <param name="relatedSymbols">List to add discovered relationships to.</param>
    /// <param name="seenCombinations">Set to track already processed type+label combinations.</param>
    private static void ProcessMemberType(
        ITypeSymbol type,
        string memberName,
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols,
        HashSet<(string TypeName, string? Label)> seenCombinations)
    {
        if (type is not INamedTypeSymbol { SpecialType: SpecialType.None } namedType)
        {
            return;
        }

        // Detect if this is a collection type
        var isCollection = namedType.IsGenericType &&
                           (namedType.Name.Contains("List") ||
                            namedType.Name.Contains("Collection") ||
                            namedType.Name.Contains("IEnumerable") ||
                            namedType.Name.Contains("Array") ||
                            namedType.Name.Contains("Set"));

        var cardinality = isCollection ? "*" : "1";

        var extractedTypes = ExtractTypesFromGeneric(namedType);

        foreach (var extracted in extractedTypes)
        {
            // Use simple type name for deduplication since full name might not be available
            // for generic type arguments until they're resolved
            var typeName = extracted.Name;

            // Skip if already seen this type+label combination or if it's a system type
            if (!seenCombinations.Add((typeName, memberName)) || IsSystemType(extracted))
            {
                continue;
            }

            relatedSymbols.Add((extracted, RelationshipKind.Association, memberName, cardinality));
        }
    }

    /// <summary>
    /// Extracts concrete types from a potentially generic type.
    /// For example, List&lt;Address&gt; would return [Address], and Dictionary&lt;string, User&gt; would return [User].
    /// This avoids creating nodes for generic container types.
    /// </summary>
    /// <param name="type">The type to extract from.</param>
    /// <returns>A list of concrete named type symbols.</returns>
    private static List<INamedTypeSymbol> ExtractTypesFromGeneric(INamedTypeSymbol type)
    {
        var result = new List<INamedTypeSymbol>();

        // If it's a generic type (like List<T>, Dictionary<K,V>), extract the type arguments
        if (type is { IsGenericType: true, TypeArguments.Length: > 0 })
        {
            foreach (var typeArg in type.TypeArguments)
            {
                if (typeArg is not INamedTypeSymbol { SpecialType: SpecialType.None } namedTypeArg)
                {
                    continue;
                }

                // Recursively handle nested generics
                if (namedTypeArg.IsGenericType)
                {
                    result.AddRange(ExtractTypesFromGeneric(namedTypeArg));
                }
                else if (!IsSystemType(namedTypeArg))
                {
                    result.Add(namedTypeArg);
                }
            }
        }
        else if (!IsSystemType(type))
        {
            // Not a generic type, return the type itself if it's not a system type
            result.Add(type);
        }

        return result;
    }

    /// <summary>
    /// Determines if a type is a system/framework type that should be excluded from diagrams.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is from System namespace or common framework namespaces.</returns>
    private static bool IsSystemType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        return ns.StartsWith("System", StringComparison.Ordinal) ||
               ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
               type.SpecialType != SpecialType.None;
    }

    /// <summary>
    /// Processes a list of related symbols asynchronously, updating the analysis context with their relationships
    /// and adding unresolved symbols to the analysis queue for further processing.
    /// </summary>
    /// <param name="relatedSymbols">
    /// A list of tuples containing related symbols and their corresponding relationship kinds.
    /// </param>
    /// <param name="fullName">The fully qualified name of the current type being analyzed.</param>
    /// <param name="depth">The current depth of the analysis in the type hierarchy.</param>
    /// <param name="typesToAnalyze">
    /// A queue of types to be analyzed, where each item is a tuple containing a symbol and its depth.
    /// </param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    private static async Task ProcessRelatedTypesAsync(
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols,
        string fullName,
        int depth,
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context)
    {
        // First, resolve all unique symbols to ensure consistency
        var resolvedSymbolsCache = new Dictionary<string, INamedTypeSymbol?>();

        foreach (var (relatedSymbol, kind, label, cardinality) in relatedSymbols)
        {
            var symbolKey = relatedSymbol.Name; // Use simple name as key

            // Resolve symbol only once per unique type
            if (!resolvedSymbolsCache.TryGetValue(symbolKey, out var resolvedSymbol))
            {
                resolvedSymbol = await ResolveRelatedSymbolAsync(relatedSymbol, context);
                resolvedSymbolsCache[symbolKey] = resolvedSymbol;
            }

            var symbolToUse = resolvedSymbol ?? relatedSymbol;
            var relatedFullName = GetFullyQualifiedName(symbolToUse);

            context.Relationships.Add(new Relationship(fullName, relatedFullName, kind, label, cardinality));

            // Only enqueue if we haven't analyzed this type yet
            if (context.AnalyzedTypeFullNames.Contains(relatedFullName))
            {
                continue;
            }

            if (resolvedSymbol is null)
            {
                continue;
            }

            // Only enqueue once per unique type (check if already in queue would be complex, 
            // but the AnalyzedTypeFullNames check in ProcessTypeQueueAsync handles duplicates)
            typesToAnalyze.Enqueue((resolvedSymbol, depth + 1));
        }
    }

    /// <summary>
    /// Resolves a related symbol asynchronously by checking its location and updating the analysis context.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> representing the related symbol to resolve.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved <see cref="INamedTypeSymbol"/>
    /// if found, or null if the symbol could not be resolved.
    /// </returns>
    private static async Task<INamedTypeSymbol?> ResolveRelatedSymbolAsync(
        INamedTypeSymbol relatedSymbol,
        AnalysisContext context)
    {
        if (relatedSymbol.Locations.Any(l => l.IsInSource))
        {
            return relatedSymbol;
        }

        var foundFile =
            await WorkspaceTypeDiscovery.FindTypeDefinitionFileAsync(relatedSymbol.Name, context.StartDirectory);

        if (foundFile == null)
        {
            AddExternalType(relatedSymbol, context);
            return null;
        }

        if (context.Compilation.SyntaxTrees.Any(t => t.FilePath == foundFile))
        {
            return relatedSymbol;
        }

        return await LoadAndResolveSymbolAsync(relatedSymbol, foundFile, context);
    }

    /// <summary>
    /// Adds an external type to the analysis context. This method is used when a related symbol
    /// is determined to be external (not defined in the current project) and needs to be added
    /// to the list of analyzed types.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> representing the external type to add.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    private static void AddExternalType(
        INamedTypeSymbol relatedSymbol,
        AnalysisContext context)
    {
        var fullName = GetFullyQualifiedName(relatedSymbol);
        context.Types.Add(new TypeDefinition(
            relatedSymbol.Name,
            relatedSymbol.ContainingNamespace.ToDisplayString(),
            fullName,
            MapKind(relatedSymbol),
            [],
            true));
        context.AnalyzedTypeFullNames.Add(fullName);
    }

    /// <summary>
    /// Loads and resolves a related symbol from a specified file and updates the analysis context.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> representing the related symbol to resolve.</param>
    /// <param name="foundFile">The file path where the related symbol is defined.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved <see cref="INamedTypeSymbol"/> 
    /// if found, or the original <paramref name="relatedSymbol"/> if the symbol could not be resolved.
    /// </returns>
    private static async Task<INamedTypeSymbol?> LoadAndResolveSymbolAsync(
        INamedTypeSymbol relatedSymbol,
        string foundFile,
        AnalysisContext context)
    {
        var relatedCode = await File.ReadAllTextAsync(foundFile);
        var relatedTree = CSharpSyntaxTree.ParseText(relatedCode, path: foundFile);
        context.Compilation = context.Compilation.AddSyntaxTrees(relatedTree);

        var newSemanticModel = context.Compilation.GetSemanticModel(relatedTree);
        var root = await relatedTree.GetRootAsync();
        var decl = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == relatedSymbol.Name);

        return decl != null ? newSemanticModel.GetDeclaredSymbol(decl) ?? relatedSymbol : relatedSymbol;
    }

    /// <summary>
    /// Analyzes a given Roslyn named type symbol and extracts its type definition, 
    /// including its name, namespace, full name, kind, and members.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to be analyzed.</param>
    /// <returns>
    /// A <see cref="TypeDefinition"/> object containing the analyzed type's details, 
    /// such as its name, namespace, full name, kind, and members.
    /// </returns>
    private static TypeDefinition AnalyzeType(INamedTypeSymbol symbol)
    {
        var members = new List<MemberDefinition>();

        foreach (var member in symbol.GetMembers().Where(member => !member.IsImplicitlyDeclared))
        {
            switch (member)
            {
                case IPropertySymbol prop:
                    members.Add(new MemberDefinition(
                        prop.Name,
                        prop.Type.ToDisplayString(),
                        MapAccessibility(prop.DeclaredAccessibility),
                        MemberKind.Property));
                    break;
                case IFieldSymbol { IsImplicitlyDeclared: false } field:
                    members.Add(new MemberDefinition(
                        field.Name,
                        field.Type.ToDisplayString(),
                        MapAccessibility(field.DeclaredAccessibility),
                        MemberKind.Field));
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        var parameters = method.Parameters
                            .Select(p => new ParameterDefinition(p.Name, p.Type.ToDisplayString())).ToList();
                        members.Add(new MemberDefinition(
                            method.Name,
                            method.ReturnType.ToDisplayString(),
                            MapAccessibility(method.DeclaredAccessibility),
                            MemberKind.Method,
                            parameters));
                        break;
                    }
            }
        }

        return new TypeDefinition(
            symbol.Name,
            symbol.ContainingNamespace.ToDisplayString(),
            GetFullyQualifiedName(symbol),
            MapKind(symbol),
            members);
    }

    /// <summary>
    /// Gets the fully qualified name of a type symbol, including its namespace.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> to get the fully qualified name for.</param>
    /// <returns>The fully qualified name in the format Namespace.TypeName.</returns>
    private static string GetFullyQualifiedName(INamedTypeSymbol symbol)
    {
        // Use ToDisplayString with FullyQualifiedFormat to get the complete name
        // This ensures we get the full namespace even for symbols not yet in compilation
        var fullyQualifiedName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Remove the leading "global::" prefix if present
        if (fullyQualifiedName.StartsWith("global::"))
        {
            fullyQualifiedName = fullyQualifiedName[8..];
        }


        return fullyQualifiedName;
    }

    /// <summary>
    /// Maps the Roslyn <see cref="Accessibility"/> of a symbol to the corresponding <see cref="Visibility"/>.
    /// </summary>
    /// <param name="accessibility">The <see cref="Accessibility"/> value representing the access level of a symbol.</param>
    /// <returns>
    /// A <see cref="Visibility"/> value that corresponds to the provided <see cref="Accessibility"/>.
    /// </returns>
    private static Visibility MapAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => Visibility.Public,
            Accessibility.Protected => Visibility.Protected,
            Accessibility.Internal => Visibility.Internal,
            _ => Visibility.Private
        };
    }

    /// <summary>
    /// Maps the Roslyn <see cref="Microsoft.CodeAnalysis.TypeKind"/> of a symbol to the corresponding <see cref="ModelTypeKind"/>.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to be mapped.</param>
    /// <returns>
    /// A <see cref="ModelTypeKind"/> value that corresponds to the <see cref="Microsoft.CodeAnalysis.TypeKind"/> of the provided symbol.
    /// </returns>
    private static ModelTypeKind MapKind(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind switch
        {
            TypeKind.Interface => ModelTypeKind.Interface,
            TypeKind.Struct => ModelTypeKind.Struct,
            TypeKind.Enum => ModelTypeKind.Enum,
            _ => ModelTypeKind.Class
        };
    }

    /// <summary>
    /// Represents the context for the analysis process, containing information about analyzed types,
    /// their relationships, and the compilation context.
    /// </summary>
    private sealed class AnalysisContext
    {
        /// <summary>
        /// A set of fully qualified names of types that have already been analyzed.
        /// </summary>
        public required HashSet<string> AnalyzedTypeFullNames { get; init; }

        /// <summary>
        /// A list of type definitions discovered during the analysis.
        /// </summary>
        public required List<TypeDefinition> Types { get; init; }

        /// <summary>
        /// A list of relationships between the analyzed types.
        /// </summary>
        public required List<Relationship> Relationships { get; init; }

        /// <summary>
        /// The Roslyn CSharpCompilation object used for semantic analysis.
        /// </summary>
        public required CSharpCompilation Compilation { get; set; }

        /// <summary>
        /// The starting directory for the analysis process.
        /// </summary>
        public required string StartDirectory { get; init; }
    }

    /// <summary>
    /// Represents the options for configuring the analysis process, such as depth and inclusion of relationships.
    /// </summary>
    private sealed class AnalysisOptions
    {
        /// <summary>
        /// The maximum depth of type relationships to analyze.
        /// </summary>
        public required int MaxDepth { get; init; }

        /// <summary>
        /// Indicates whether inheritance relationships should be included in the analysis.
        /// </summary>
        public required bool IncludeInheritance { get; init; }

        /// <summary>
        /// Indicates whether dependency relationships should be included in the analysis.
        /// </summary>
        public required bool IncludeDependencies { get; init; }
    }
}