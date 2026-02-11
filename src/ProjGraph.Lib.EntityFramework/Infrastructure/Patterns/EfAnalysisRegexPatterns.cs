using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using System.Text.RegularExpressions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

/// <summary>
/// Provides centralized compiled regex patterns for Entity Framework analysis operations.
/// All regex patterns used throughout the EF analysis services are consolidated here for
/// better maintainability and reusability.
/// </summary>
public static partial class EfAnalysisRegexPatterns
{
    #region Entity Configuration Patterns

    /// <summary>
    /// A regex pattern to extract entity names from Entity configuration calls.
    /// </summary>
    /// <remarks>
    /// This pattern matches both generic and string-based entity declarations:
    /// - Generic: Entity&lt;EntityName&gt;
    /// - String: Entity("EntityName")
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.EntityPattern)]
    public static partial Regex EntityNameRegex();

    /// <summary>
    /// A regex pattern to split configuration sections by Entity calls.
    /// </summary>
    /// <remarks>
    /// This pattern identifies the start of new entity configuration sections.
    /// Used to parse OnModelCreating and BuildModel method content.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.EntitySplitPattern)]
    public static partial Regex EntitySplitRegex();

    /// <summary>
    /// A regex pattern to match Entity configuration calls in EfAnalysisService.
    /// </summary>
    /// <remarks>
    /// This pattern is used to extract entity information from configuration text.
    /// Supports both generic and string literal entity declarations.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.EntityMatchPattern)]
    public static partial Regex EntityMatchRegex();

    #endregion

    #region Relationship Patterns

    /// <summary>
    /// A regex pattern to match shadow relationships in Fluent API configurations.
    /// </summary>
    /// <remarks>
    /// Matches patterns like: HasOne&lt;TypeName&gt;().WithMany() or HasMany&lt;TypeName&gt;().WithOne()
    /// Used to identify relationships that don't have explicit navigation properties.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.ShadowRelationshipPattern)]
    public static partial Regex ShadowRelationshipRegex();

    #endregion

    #region Property and Method Patterns

    /// <summary>
    /// A regex pattern to extract property names from lambda expressions.
    /// </summary>
    /// <remarks>
    /// Matches lambda expressions in the format: (entity) => entity.PropertyName
    /// Used to parse Property() method calls in Fluent API configurations.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.LambdaPropertyPattern)]
    public static partial Regex PropertyLambdaRegex();

    /// <summary>
    /// A regex pattern to match method calls with arguments in method chains.
    /// </summary>
    /// <remarks>
    /// Captures method names and their arguments from Fluent API method chains.
    /// Example: .HasMaxLength(100) captures "HasMaxLength" and "100"
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.MethodCallPattern)]
    public static partial Regex MethodCallRegex();

    /// <summary>
    /// A regex pattern to match method names in a method chain, preceded by a dot.
    /// </summary>
    /// <remarks>
    /// This pattern matches a dot followed by optional whitespace and a word (method name).
    /// Example: In .HasMaxLength or . IsRequired, it captures HasMaxLength and IsRequired.
    /// Used to parse Fluent API method chains like entity.Property(x => x.Name).HasMaxLength(100).IsRequired()
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.MethodNamePattern)]
    public static partial Regex MethodChainRegex();

    #endregion

    #region Table and Column Patterns

    /// <summary>
    /// A regex pattern to extract table names from ToTable() method calls.
    /// </summary>
    /// <remarks>
    /// Matches .ToTable("TableName") calls and extracts the table name.
    /// Used to identify custom table names in Fluent API configurations.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.ToTablePattern)]
    public static partial Regex ToTableRegex();

    #endregion

    #region String and Literal Patterns

    /// <summary>
    /// A regex pattern to extract string literals from configuration text.
    /// </summary>
    /// <remarks>
    /// This pattern captures the content within double quotes, excluding the quotes themselves.
    /// Example: In "Hello World", it captures Hello World.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.StringLiteralPattern)]
    public static partial Regex StringLiteralRegex();

    #endregion

    #region Numeric and Argument Patterns

    /// <summary>
    /// A regex pattern to extract numeric arguments from method calls.
    /// </summary>
    /// <remarks>
    /// Captures numeric values within parentheses.
    /// Example: In nvarchar(30) or decimal(18,2), it captures 30 from the first match.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.NumericArgumentPattern)]
    public static partial Regex NumberInParensRegex();

    #endregion

    #region SQL Type Patterns

    /// <summary>
    /// A regex pattern to match decimal types with precision and scale.
    /// </summary>
    /// <remarks>
    /// Matches decimal(precision, scale) format and captures both precision and scale values.
    /// Used to extract precision and scale constraints from ColumnAttribute TypeName arguments.
    /// The regex captures two groups:
    /// - Group 1: The precision (number of total digits)
    /// - Group 2: The scale (number of digits after the decimal point)
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.SqlTypePatterns.DecimalPattern)]
    public static partial Regex DecimalPrecisionRegex();

    #endregion SQL Type Patterns
}
