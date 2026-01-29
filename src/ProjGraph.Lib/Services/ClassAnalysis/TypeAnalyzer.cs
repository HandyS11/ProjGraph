using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ModelTypeKind = ProjGraph.Core.Models.TypeKind;
using TypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace ProjGraph.Lib.Services.ClassAnalysis;

/// <summary>
/// Provides methods for analyzing individual type symbols and extracting their definitions.
/// </summary>
internal static class TypeAnalyzer
{
    private static readonly SymbolDisplayFormat ShortNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Analyzes a type symbol and extracts its definition including members (properties, fields, and methods).
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze.</param>
    /// <returns>
    /// A <see cref="TypeDefinition"/> object containing the analyzed type's metadata and members.
    /// </returns>
    public static TypeDefinition AnalyzeType(INamedTypeSymbol symbol)
    {
        var members = new List<MemberDefinition>();
        var isEnum = symbol.TypeKind == TypeKind.Enum;

        foreach (var member in symbol.GetMembers().Where(member => !member.IsImplicitlyDeclared))
        {
            switch (member)
            {
                case IPropertySymbol prop:
                    members.Add(new MemberDefinition(
                        prop.Name,
                        prop.Type.ToDisplayString(ShortNameFormat),
                        MapAccessibility(prop.DeclaredAccessibility),
                        MemberKind.Property));
                    break;
                case IFieldSymbol { IsImplicitlyDeclared: false } field:
                    // For enums, only show the field name without the type
                    var fieldType = isEnum ? string.Empty : field.Type.ToDisplayString(ShortNameFormat);
                    members.Add(new MemberDefinition(
                        field.Name,
                        fieldType,
                        MapAccessibility(field.DeclaredAccessibility),
                        MemberKind.Field));
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        var parameters = method.Parameters
                            .Select(p => new ParameterDefinition(p.Name, p.Type.ToDisplayString(ShortNameFormat)))
                            .ToList();
                        members.Add(new MemberDefinition(
                            method.Name,
                            method.ReturnType.ToDisplayString(ShortNameFormat),
                            MapAccessibility(method.DeclaredAccessibility),
                            MemberKind.Method,
                            parameters));
                        break;
                    }
            }
        }

        return new TypeDefinition(
            symbol.ToDisplayString(ShortNameFormat),
            symbol.ContainingNamespace.ToDisplayString(),
            GetFullyQualifiedName(symbol),
            MapKind(symbol),
            members,
            symbol.IsAbstract);
    }

    /// <summary>
    /// Gets the fully qualified name of a type symbol, including its namespace.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> to get the fully qualified name for.</param>
    /// <returns>The fully qualified name in the format Namespace.TypeName.</returns>
    public static string GetFullyQualifiedName(INamedTypeSymbol symbol)
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
    public static ModelTypeKind MapKind(INamedTypeSymbol symbol)
    {
        // Check if it's a record (records are a special kind of class or struct)
        if (symbol.IsRecord)
        {
            return ModelTypeKind.Record;
        }

        return symbol.TypeKind switch
        {
            TypeKind.Interface => ModelTypeKind.Interface,
            TypeKind.Struct => ModelTypeKind.Struct,
            TypeKind.Enum => ModelTypeKind.Enum,
            _ => ModelTypeKind.Class
        };
    }
}