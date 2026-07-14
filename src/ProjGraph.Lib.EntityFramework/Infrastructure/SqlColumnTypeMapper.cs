namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Maps a relational SQL column type (as passed to <c>HasColumnType</c>) to the CLR type EF Core
/// would use for it. Only unambiguous, non-<c>string</c> mappings are returned; string-backed column
/// types (e.g. <c>nvarchar</c>) and unknown types return <c>null</c> so the caller keeps its existing
/// (usually <c>string</c>) type rather than guessing.
/// </summary>
internal static class SqlColumnTypeMapper
{
    private static readonly IReadOnlyDictionary<string, string> SqlToClr =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["decimal"] = "decimal",
            ["numeric"] = "decimal",
            ["money"] = "decimal",
            ["smallmoney"] = "decimal",
            ["int"] = "int",
            ["integer"] = "int",
            ["bigint"] = "long",
            ["smallint"] = "short",
            ["tinyint"] = "byte",
            ["bit"] = "bool",
            ["uniqueidentifier"] = "Guid",
            ["float"] = "double",
            ["real"] = "float",
            ["datetime"] = "DateTime",
            ["datetime2"] = "DateTime",
            ["smalldatetime"] = "DateTime",
            ["datetimeoffset"] = "DateTimeOffset",
            ["date"] = "DateOnly",
            ["time"] = "TimeOnly"
        };

    /// <summary>
    /// Returns the CLR type name for a SQL column type, or <c>null</c> when the type maps to a string
    /// or is not recognised.
    /// </summary>
    /// <param name="columnType">The raw column type argument, e.g. <c>decimal(18,2)</c> or <c>bit</c>.</param>
    public static string? ToClrType(string columnType)
    {
        if (string.IsNullOrWhiteSpace(columnType))
        {
            return null;
        }

        // Take the leading type keyword, dropping any size/precision suffix and surrounding quotes:
        // "decimal(18,2)" -> "decimal", "datetime2" keeps its trailing digit.
        var trimmed = columnType.Trim().TrimStart('"').AsSpan();
        var length = 0;
        while (length < trimmed.Length && (char.IsLetter(trimmed[length]) || char.IsDigit(trimmed[length])))
        {
            length++;
        }

        var keyword = trimmed[..length].ToString();
        return SqlToClr.GetValueOrDefault(keyword);
    }
}
