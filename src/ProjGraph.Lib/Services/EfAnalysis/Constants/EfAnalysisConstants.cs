namespace ProjGraph.Lib.Services.EfAnalysis.Constants;

/// <summary>
/// Contains constant values used throughout the EF Analysis services.
/// </summary>
public static class EfAnalysisConstants
{
    /// <summary>
    /// Contains constant values for .NET type names.
    /// </summary>
    public static class DataTypes
    {
        public const string Int = "int";
        public const string Int32 = "Int32";
        public const string Int64 = "Int64";
        public const string String = "string";
        public const string Bool = "bool";
        public const string Boolean = "Boolean";
        public const string Guid = "Guid";
        public const string DateTime = "DateTime";
        public const string DateTimeOffset = "DateTimeOffset";
        public const string TimeSpan = "TimeSpan";
        public const string Decimal = "decimal";
        public const string Double = "double";
        public const string Float = "float";
        public const string Long = "long";
        public const string Single = "Single";
        public const string Short = "short";

        /// <summary>
        /// A set of common .NET value types that are treated as having a default value in EF.
        /// </summary>
        public static readonly HashSet<string> ValueTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            Int,
            Int32,
            Int64,
            Long,
            Bool,
            Boolean,
            Guid,
            DateTime,
            DateTimeOffset,
            TimeSpan,
            Decimal,
            Double,
            Float,
            Single,
            Short
        };
    }

    /// <summary>
    /// Contains constant values for Entity Framework method names.
    /// </summary>
    public static class EfMethods
    {
        // Relationship methods
        public const string HasOne = "HasOne";
        public const string HasMany = "HasMany";
        public const string WithOne = "WithOne";
        public const string WithMany = "WithMany";

        // Configuration methods
        public const string Entity = "Entity";
        public const string ToTable = "ToTable";
        public const string Property = "Property";
        public const string HasKey = "HasKey";
        public const string HasForeignKey = "HasForeignKey";
        public const string IsRequired = "IsRequired";
        public const string HasMaxLength = "HasMaxLength";
        public const string HasPrecision = "HasPrecision";
        public const string HasColumnType = "HasColumnType";
        public const string HasDefaultValue = "HasDefaultValue";
        public const string HasDefaultValueSql = "HasDefaultValueSql";
        public const string UsingEntity = "UsingEntity";

        // DbContext methods
        public const string OnModelCreating = "OnModelCreating";
        public const string BuildModel = "BuildModel";
    }

    /// <summary>
    /// Contains constant values for Entity Framework attribute names.
    /// </summary>
    public static class EfAttributes
    {
        public const string KeyAttribute = "KeyAttribute";
        public const string Key = "Key";
        public const string PrimaryKeyAttribute = "PrimaryKeyAttribute";
        public const string PrimaryKey = "PrimaryKey";
        public const string RequiredAttribute = "RequiredAttribute";
        public const string MaxLengthAttribute = "MaxLengthAttribute";
        public const string StringLengthAttribute = "StringLengthAttribute";
        public const string ColumnAttribute = "ColumnAttribute";
        public const string DbContextAttribute = "DbContextAttribute";
    }

    /// <summary>
    /// Contains constant values for common property and method names.
    /// </summary>
    public static class CommonNames
    {
        public const string Id = "Id";
        public const string TypeName = "TypeName";
        public const string Nameof = "nameof";
        public const string DbSet = "DbSet";
        public const string DbContext = "DbContext";
        public const string ModelSnapshot = "ModelSnapshot";
        public const string System = "System";
        public const string Nullable = "Nullable";
    }

    /// <summary>
    /// Contains constant values for collection type names.
    /// </summary>
    public static class CollectionTypes
    {
        public const string ICollection = "ICollection";
        public const string IList = "IList";
        public const string List = "List";
        public const string HashSet = "HashSet";
        public const string ISet = "ISet";
        public const string IEnumerable = "IEnumerable";
    }

    /// <summary>
    /// Contains constant values for string suffixes and patterns.
    /// </summary>
    public static class Suffixes
    {
        public const string IdSuffix = "Id";
    }

    /// <summary>
    /// Contains constant values for relationship type keys and delimiters.
    /// </summary>
    public static class RelationshipKeys
    {
        public const string Delimiter = "-";
    }

    /// <summary>
    /// Contains constant values for file patterns and extensions.
    /// </summary>
    public static class FilePatterns
    {
        public const string CSharpFiles = "*.cs";
        public const string CSharpExtension = ".cs";
    }

    /// <summary>
    /// Contains constant values for SQL type patterns and formats.
    /// </summary>
    public static class SqlTypePatterns
    {
        public const string DecimalPattern = @"decimal\((\d+),\s*(\d+)\)";
    }

    /// <summary>
    /// Contains constant values for regex patterns used in Fluent API parsing.
    /// </summary>
    public static class FluentApiPatterns
    {
        public const string EntityPattern = """Entity(?:<([^>]+)>|\("([^"]+)"(?:,\s*[^)]+)?\))""";
        public const string EntitySplitPattern = @"\.Entity(?=[<(])";
        public const string EntityMatchPattern = """\.Entity\s*(?:<([^>]+)>|\(\s*"([^"]+)"\s*)""";
        public const string ShadowRelationshipPattern = @"(HasOne|HasMany)<(\w+)>\(\s*\)\s*\.(WithOne|WithMany)\(\s*\)";
        public const string LambdaPropertyPattern = @"^\s*\(?\s*(\w+)\s*\)?\s*=>\s*\1\.(\w+)\s*$";
        public const string MethodCallPattern = @"\.(\w+(?:<[^>]+>)?)\(([^()]*(?:\([^()]*\)[^()]*)*)\)";
        public const string ToTablePattern = """\.ToTable\(\"([^\"]+)\"\)""";
        public const string StringLiteralPattern = "\"([^\"]+)\"";
        public const string MethodNamePattern = @"\.\s*(\w+)";
        public const string NumericArgumentPattern = @"\((\d+)\)";
    }
}