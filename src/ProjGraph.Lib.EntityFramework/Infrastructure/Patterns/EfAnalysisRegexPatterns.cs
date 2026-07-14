using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using System.Text.RegularExpressions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

/// <summary>
/// Compiled regex patterns for parsing SQL type strings (column type names, attribute arguments).
/// These parse string *values*, not C# source — source-level Fluent API parsing is done on the syntax
/// tree by the fluent walkers.
/// </summary>
public static partial class EfAnalysisRegexPatterns
{
    /// <summary>
    /// A regex pattern to extract numeric arguments from SQL type names.
    /// </summary>
    /// <remarks>
    /// Captures numeric values within parentheses.
    /// Example: In nvarchar(30) or decimal(18,2), it captures 30 from the first match.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.NumericArgumentPattern)]
    public static partial Regex NumberInParensRegex();

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
}
