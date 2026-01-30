using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis.Extensions;
using System.Text.RegularExpressions;

namespace ProjGraph.Lib.Services.EfAnalysis;

/// <summary>
/// Provides methods for analyzing entity type symbols and extracting metadata such as properties, 
/// primary keys, and constraints. This class is implemented as a partial class to allow for 
/// extension in other files.
/// </summary>
public static partial class EntityAnalyzer
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
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
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
        return compilation.GlobalNamespace
            .GetAllNamedTypes()
            .FirstOrDefault(t => t.Name == entity.Name);
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

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var arg in currentType.GetAttributes()
                         .Where(attribute => attribute.AttributeClass?.Name == "PrimaryKeyAttribute")
                         .SelectMany(attribute => attribute.ConstructorArguments))
            {
                if (arg.Value is string pkName)
                {
                    primaryKeyNames.Add(pkName);
                }
            }

            currentType = currentType.BaseType;
        }

        return primaryKeyNames;
    }

    /// <summary>
    /// Creates an <see cref="EfProperty"/> instance for the specified property symbol, entity type, and current type.
    /// </summary>
    /// <param name="prop">The property symbol representing the property to analyze.</param>
    /// <param name="entityType">The entity type symbol to which the property belongs.</param>
    /// <param name="currentType">The current type symbol being analyzed (may be a base type of the entity).</param>
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
            IsRequired = !prop.Type.IsNullable()
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
        if (propName.Equals("Id", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Property named "{EntityName}Id" pattern
        if (propName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
        {
            return propName.Equals($"{entityTypeName}Id", StringComparison.OrdinalIgnoreCase) ||
                   propName.Equals($"{currentTypeName}Id", StringComparison.OrdinalIgnoreCase);
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
            case "RequiredAttribute":
                efProperty.IsRequired = true;
                break;

            case "MaxLengthAttribute":
            case "StringLengthAttribute":
                if (attribute.ConstructorArguments.Length > 0 &&
                    attribute.ConstructorArguments[0].Value is int maxLength)
                {
                    efProperty.MaxLength = maxLength;
                }

                break;

            case "ColumnAttribute":
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
            if (namedArg is not { Key: "TypeName", Value.Value: string typeName })
            {
                continue;
            }

            var match = DecimalPrecisionRegex().Match(typeName);
            if (!match.Success)
            {
                continue;
            }

            efProperty.Precision = int.Parse(match.Groups[1].Value);
            efProperty.Scale = int.Parse(match.Groups[2].Value);
        }
    }

    /// <summary>
    /// Defines a regular expression to match a decimal type with precision and scale in the format "decimal(precision, scale)".
    /// </summary>
    /// <remarks>
    /// The regular expression captures two groups:
    /// <list type="bullet">
    /// <item>
    /// <description>The first group captures the precision (number of total digits).</description>
    /// </item>
    /// <item>
    /// <description>The second group captures the scale (number of digits after the decimal point).</description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <returns>A <see cref="Regex"/> object that matches the specified decimal format.</returns>
    [GeneratedRegex(@"decimal\((\d+),\s*(\d+)\)")]
    private static partial Regex DecimalPrecisionRegex();
}