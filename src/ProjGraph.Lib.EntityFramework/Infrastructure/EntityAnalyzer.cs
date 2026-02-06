using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;
using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Provides methods for analyzing entity type symbols and extracting metadata such as properties, 
/// primary keys, and constraints. This class is implemented as a partial class to allow for 
/// extension in other files.
/// </summary>
public static class EntityAnalyzer
{
    /// <summary>
    /// Analyzes the specified entity type symbol and extracts its properties, primary keys, and constraints.
    /// </summary>
    /// <param name="type">The <see cref="INamedTypeSymbol"/> representing the entity type to analyze.</param>
    /// <returns>
    /// An <see cref="EfEntity"/> object containing metadata about the entity, including its name and properties.
    /// </returns>
    /// <remarks>
    /// This method traverses the inheritance hierarchy of the given entity type symbol to collect all properties.
    /// It skips navigation properties and ensures that each property is added only once.
    /// For each property, it determines whether it is a primary key, a foreign key, and applies any constraints.
    /// </remarks>
    public static EfEntity AnalyzeEntity(INamedTypeSymbol type)
    {
        var entity = new EfEntity { Name = type.Name };
        var addedProperties = new HashSet<string>();
        var primaryKeyNames = ExtractPrimaryKeyNames(type);

        var currentType = type;
        while (currentType is not null && currentType.SpecialType is not SpecialType.System_Object)
        {
            foreach (var prop in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (NavigationPropertyAnalyzer.IsNavigationProperty(prop, out _, out _))
                {
                    continue;
                }

                if (!addedProperties.Add(prop.Name))
                {
                    continue;
                }

                var efProperty = CreateEfProperty(prop, type, currentType, primaryKeyNames);
                ExtractPropertyConstraints(prop, efProperty);
                entity.Properties.Add(efProperty);
            }

            currentType = currentType.BaseType;
        }

        return entity;
    }

    /// <summary>
    /// Finds the entity symbol in the provided compilation that matches the name of the given <see cref="EfEntity"/>.
    /// </summary>
    /// <param name="entity">The <see cref="EfEntity"/> containing the name of the entity to find.</param>
    /// <param name="compilation">The <see cref="Compilation"/> object to search for the entity symbol.</param>
    /// <returns>
    /// An <see cref="INamedTypeSymbol"/> representing the entity symbol if found; otherwise, <c>null</c>.
    /// </returns>
    public static INamedTypeSymbol? FindEntitySymbol(EfEntity entity, Compilation compilation)
    {
        return compilation.GetSymbolsWithName(entity.Name, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();
    }

    /// <summary>
    /// Extracts the names of primary key properties from the specified entity type symbol.
    /// </summary>
    /// <param name="type">The entity type symbol to analyze for primary key attributes.</param>
    /// <returns>
    /// A <see cref="HashSet{T}"/> containing the names of the primary key properties.
    /// </returns>
    /// <remarks>
    /// This method traverses the inheritance hierarchy of the given entity type symbol to collect all property names
    /// marked with the <c>PrimaryKeyAttribute</c>. It inspects the attributes of each type in the hierarchy and
    /// extracts the constructor arguments of the <c>PrimaryKeyAttribute</c> that represent the primary key names.
    /// </remarks>
    private static HashSet<string> ExtractPrimaryKeyNames(INamedTypeSymbol type)
    {
        var primaryKeyNames = new HashSet<string>();
        var currentType = type;

        while (currentType is not null && currentType.SpecialType is not SpecialType.System_Object)
        {
            ExtractPrimaryKeysFromSemanticModel(currentType, primaryKeyNames);
            ExtractPrimaryKeysFromSyntax(currentType, primaryKeyNames);
            currentType = currentType.BaseType;
        }

        return primaryKeyNames;
    }

    /// <summary>
    /// Extracts primary key names from the semantic model of the specified type.
    /// </summary>
    /// <param name="type">The type symbol to analyze.</param>
    /// <param name="primaryKeyNames">The set to populate with primary key names.</param>
    private static void ExtractPrimaryKeysFromSemanticModel(INamedTypeSymbol type, HashSet<string> primaryKeyNames)
    {
        ExtractPrimaryKeysFromTypeAttributes(type, primaryKeyNames);
        ExtractPrimaryKeysFromPropertyAttributes(type, primaryKeyNames);
    }

    /// <summary>
    /// Extracts primary key names from type-level attributes.
    /// </summary>
    /// <param name="type">The type symbol to analyze.</param>
    /// <param name="primaryKeyNames">The set to populate with primary key names.</param>
    private static void ExtractPrimaryKeysFromTypeAttributes(INamedTypeSymbol type, HashSet<string> primaryKeyNames)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attrName = attribute.AttributeClass?.Name;
            if (attrName is not (EfAnalysisConstants.EfAttributes.PrimaryKeyAttribute
                or EfAnalysisConstants.EfAttributes.PrimaryKey))
            {
                continue;
            }

            foreach (var arg in attribute.ConstructorArguments)
            {
                CollectNamesFromConstant(arg, primaryKeyNames);
            }
        }
    }

    /// <summary>
    /// Extracts primary key names from property-level attributes.
    /// </summary>
    /// <param name="type">The type symbol to analyze.</param>
    /// <param name="primaryKeyNames">The set to populate with primary key names.</param>
    private static void ExtractPrimaryKeysFromPropertyAttributes(INamedTypeSymbol type, HashSet<string> primaryKeyNames)
    {
        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>()
                     .Where(p => p.GetAttributes().Any(a =>
                         a.AttributeClass?.Name is EfAnalysisConstants.EfAttributes.KeyAttribute
                             or EfAnalysisConstants.EfAttributes.Key)))
        {
            primaryKeyNames.Add(prop.Name);
        }
    }

    /// <summary>
    /// Extracts primary key names from the syntax tree of the specified type.
    /// </summary>
    /// <param name="type">The type symbol to analyze.</param>
    /// <param name="primaryKeyNames">The set to populate with primary key names.</param>
    private static void ExtractPrimaryKeysFromSyntax(INamedTypeSymbol type, HashSet<string> primaryKeyNames)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not ClassDeclarationSyntax classSyntax)
            {
                continue;
            }

            ExtractPrimaryKeysFromClassAttributes(classSyntax, primaryKeyNames);
            ExtractPrimaryKeysFromPropertySyntax(classSyntax, primaryKeyNames);
        }
    }

    /// <summary>
    /// Extracts primary key names from class-level attribute syntax.
    /// </summary>
    /// <param name="classSyntax">The class declaration syntax to analyze.</param>
    /// <param name="primaryKeyNames">The set to populate with primary key names.</param>
    private static void ExtractPrimaryKeysFromClassAttributes(ClassDeclarationSyntax classSyntax,
        HashSet<string> primaryKeyNames)
    {
        foreach (var attr in classSyntax.AttributeLists.SelectMany(al => al.Attributes))
        {
            var name = attr.Name.ToString();
            if (name is not (EfAnalysisConstants.EfAttributes.PrimaryKey
                or EfAnalysisConstants.EfAttributes.PrimaryKeyAttribute))
            {
                continue;
            }

            if (attr.ArgumentList is null)
            {
                continue;
            }

            foreach (var pkName in attr.ArgumentList.Arguments
                         .Select(arg => ExtractNameFromExpression(arg.Expression)).OfType<string>())
            {
                primaryKeyNames.Add(pkName);
            }
        }
    }

    /// <summary>
    /// Extracts primary key names from property syntax with Key attributes.
    /// </summary>
    /// <param name="classSyntax">The class declaration syntax to analyze.</param>
    /// <param name="primaryKeyNames">The set to populate with primary key names.</param>
    private static void ExtractPrimaryKeysFromPropertySyntax(ClassDeclarationSyntax classSyntax,
        HashSet<string> primaryKeyNames)
    {
        foreach (var prop in classSyntax.Members.OfType<PropertyDeclarationSyntax>()
                     .Where(p => p.AttributeLists.SelectMany(al => al.Attributes)
                         .Any(a => a.Name.ToString() is EfAnalysisConstants.EfAttributes.Key
                             or EfAnalysisConstants.EfAttributes.KeyAttribute)))
        {
            primaryKeyNames.Add(prop.Identifier.Text);
        }
    }


    private static void CollectNamesFromConstant(TypedConstant constant, HashSet<string> names)
    {
        if (constant.Kind is TypedConstantKind.Array)
        {
            foreach (var value in constant.Values)
            {
                CollectNamesFromConstant(value, names);
            }
        }
        else if (constant.Value is string name)
        {
            names.Add(name);
        }
    }

    private static string? ExtractNameFromExpression(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.Text: EfAnalysisConstants.CommonNames.Nameof }
            } invocation:
                {
                    var args = invocation.ArgumentList.Arguments;
                    if (args.Count > 0)
                    {
                        var argExpr = args[0].Expression;
                        switch (argExpr)
                        {
                            case MemberAccessExpressionSyntax ma:
                                return ma.Name.Identifier.Text;
                            case IdentifierNameSyntax id2:
                                return id2.Identifier.Text;
                        }
                    }

                    break;
                }
            case LiteralExpressionSyntax literal when
                literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Creates an <see cref="EfProperty"/> instance for the specified property symbol, entity type, and current type.
    /// </summary>
    /// <param name="prop">The property symbol representing the property to analyze.</param>
    /// <param name="entityType">The entity type symbol to which the property belongs.</param>
    /// <param name="currentType">The current type symbol being analyzed (maybe a base type of the entity).</param>
    /// <param name="primaryKeyNames">A set of explicitly defined primary key names for the entity.</param>
    /// <returns>
    /// An <see cref="EfProperty"/> object containing metadata about the property, including its name, type,
    /// whether it is a primary key, whether it is a foreign key, and whether it is required.
    /// </returns>
    private static EfProperty CreateEfProperty(
        IPropertySymbol prop,
        INamedTypeSymbol entityType,
        INamedTypeSymbol currentType,
        HashSet<string> primaryKeyNames)
    {
        var isPrimaryKey = IsPrimaryKey(prop.Name, entityType.Name, currentType.Name, primaryKeyNames);

        return new EfProperty
        {
            Name = prop.Name,
            Type = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            IsPrimaryKey = isPrimaryKey,
            IsForeignKey = false,
            IsRequired = !prop.Type.IsNullable() || prop.IsRequired,
            IsExplicitlyRequired = prop.IsRequired,
            IsValueType = prop.Type.IsEfValueType()
        };
    }

    /// <summary>
    /// Determines whether the specified property name represents a primary key.
    /// </summary>
    /// <param name="propName">The name of the property to check.</param>
    /// <param name="entityTypeName">The name of the entity type to which the property belongs.</param>
    /// <param name="currentTypeName">The name of the current type being analyzed.</param>
    /// <param name="primaryKeyNames">A set of explicitly defined primary key names.</param>
    /// <returns>
    /// <c>true</c> if the property is a primary key based on the following conditions:
    /// <list type="number">
    /// <item>
    /// <description>The property name is explicitly specified in the <c>primaryKeyNames</c> set.</description>
    /// </item>
    /// <item>
    /// <description>The property name is "Id" (following EF Core convention).</description>
    /// </item>
    /// <item>
    /// <description>The property name matches the pattern "{EntityName}Id" or "{CurrentTypeName}Id".</description>
    /// </item>
    /// </list>
    /// Otherwise, returns <c>false</c>.
    /// </returns>
    private static bool IsPrimaryKey(
        string propName,
        string entityTypeName,
        string currentTypeName,
        HashSet<string> primaryKeyNames)
    {
        // 1. Explicitly specified in [PrimaryKey] attribute
        if (primaryKeyNames.Contains(propName))
        {
            return true;
        }

        // 2. Property named "Id" (EF Core convention)
        if (propName.Equals(EfAnalysisConstants.CommonNames.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Property named "{EntityName}Id" pattern
        if (propName.EndsWith(EfAnalysisConstants.CommonNames.Id, StringComparison.OrdinalIgnoreCase))
        {
            return propName.Equals($"{entityTypeName}{EfAnalysisConstants.CommonNames.Id}",
                       StringComparison.OrdinalIgnoreCase) ||
                   propName.Equals($"{currentTypeName}{EfAnalysisConstants.CommonNames.Id}",
                       StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Extracts constraints from the attributes of a given property symbol and applies them to the specified <see cref="EfProperty"/>.
    /// </summary>
    /// <param name="prop">The property symbol whose attributes will be analyzed.</param>
    /// <param name="efProperty">The <see cref="EfProperty"/> to which the extracted constraints will be applied.</param>
    /// <remarks>
    /// This method iterates through all attributes of the provided property symbol and applies the corresponding
    /// constraints to the <paramref name="efProperty"/> by invoking the <see cref="ApplyAttributeConstraint"/> method.
    /// </remarks>
    private static void ExtractPropertyConstraints(IPropertySymbol prop, EfProperty efProperty)
    {
        foreach (var attribute in prop.GetAttributes())
        {
            ApplyAttributeConstraint(attribute, efProperty);
        }
    }

    /// <summary>
    /// Applies constraints to the specified <see cref="EfProperty"/> based on the provided attribute data.
    /// </summary>
    /// <param name="attribute">The attribute data containing metadata about the property.</param>
    /// <param name="efProperty">The <see cref="EfProperty"/> to which the constraints will be applied.</param>
    /// <remarks>
    /// This method processes specific attribute types and applies their constraints to the <paramref name="efProperty"/>:
    /// <list type="bullet">
    /// <item>
    /// <description><c>RequiredAttribute</c>: Marks the property as required.</description>
    /// </item>
    /// <item>
    /// <description><c>MaxLengthAttribute</c> or <c>StringLengthAttribute</c>: Sets the maximum length of the property if specified.</description>
    /// </item>
    /// <item>
    /// <description><c>ColumnAttribute</c>: Extracts precision and scale constraints from the "TypeName" argument and applies them to the property.</description>
    /// </item>
    /// </list>
    /// </remarks>
    private static void ApplyAttributeConstraint(AttributeData attribute, EfProperty efProperty)
    {
        var attrName = attribute.AttributeClass?.Name;

        switch (attrName)
        {
            case EfAnalysisConstants.EfAttributes.KeyAttribute:
            case EfAnalysisConstants.EfAttributes.Key:
                efProperty.IsPrimaryKey = true;
                break;

            case EfAnalysisConstants.EfAttributes.RequiredAttribute:
                efProperty.IsRequired = true;
                efProperty.IsExplicitlyRequired = true;
                break;

            case EfAnalysisConstants.EfAttributes.MaxLengthAttribute:
            case EfAnalysisConstants.EfAttributes.StringLengthAttribute:
                if (attribute.ConstructorArguments.Length > 0 &&
                    attribute.ConstructorArguments[0].Value is int maxLength)
                {
                    efProperty.MaxLength = maxLength;
                }

                break;

            case EfAnalysisConstants.EfAttributes.ColumnAttribute:
                ExtractColumnTypeNameConstraints(attribute, efProperty);
                break;
        }
    }

    /// <summary>
    /// Extracts precision and scale constraints from the "TypeName" argument of a "ColumnAttribute" and applies them to the specified <see cref="EfProperty"/>.
    /// </summary>
    /// <param name="attribute">The attribute data containing the "TypeName" argument.</param>
    /// <param name="efProperty">The <see cref="EfProperty"/> to which the constraints will be applied.</param>
    /// <remarks>
    /// This method uses a regular expression to parse the "TypeName" argument of the attribute. If the argument matches
    /// the pattern for a decimal type with precision and scale (e.g., "decimal(10, 2)"), the precision and scale values
    /// are extracted and assigned to the corresponding properties of the <paramref name="efProperty"/>.
    /// </remarks>
    private static void ExtractColumnTypeNameConstraints(AttributeData attribute, EfProperty efProperty)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg is not { Key: EfAnalysisConstants.CommonNames.TypeName, Value.Value: string typeName })
            {
                continue;
            }

            var match = EfAnalysisRegexPatterns.DecimalPrecisionRegex().Match(typeName);
            if (!match.Success)
            {
                continue;
            }

            efProperty.Precision = int.Parse(match.Groups[1].Value);
            efProperty.Scale = int.Parse(match.Groups[2].Value);
        }
    }
}