using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

/// <summary>
/// Contains constant values used throughout the EF Analysis services.
/// </summary>
public static class EfAnalysisConstants
{
    /// <summary>
    /// Contains constant values for .NET type names.
    /// </summary>
    internal static class DataTypes
    {
        public const string Guid = "Guid";
        public const string Int = "int";
        public const string StringTypeName = "string";
        private const string Int32 = "Int32";
        private const string Int64 = "Int64";
        private const string Bool = "bool";
        private const string Boolean = "Boolean";
        private const string DateTime = "DateTime";
        private const string DateTimeOffset = "DateTimeOffset";
        private const string TimeSpan = "TimeSpan";
        private const string Decimal = "decimal";
        private const string Double = "double";
        private const string Float = "float";
        private const string Long = "long";
        private const string Single = "Single";
        private const string Short = "short";
        private const string Byte = "byte";
        private const string SByte = "sbyte";
        private const string UShort = "ushort";
        private const string UInt = "uint";
        private const string UInt32 = "UInt32";
        private const string ULong = "ulong";
        private const string UInt64 = "UInt64";
        private const string Char = "char";
        private const string DateOnly = "DateOnly";
        private const string TimeOnly = "TimeOnly";
        private const string Uri = "Uri";

        /// <summary>
        /// A set of common .NET value types that are treated as having a default value in EF.
        /// </summary>
        public static readonly IReadOnlySet<string> ValueTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
            DateOnly,
            TimeOnly,
            Uri,
            Decimal,
            Double,
            Float,
            Single,
            Short,
            Byte,
            SByte,
            UShort,
            UInt,
            UInt32,
            ULong,
            UInt64,
            Char
        };

        /// <summary>
        /// A set of all types treated as primitive by EF Core (ValueTypes + String).
        /// </summary>
        public static readonly IReadOnlySet<string> AllPrimitiveTypes =
            new HashSet<string>(ValueTypes.Concat([StringTypeName]), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Contains constant values for Entity Framework method names.
    /// </summary>
    internal static class EfMethods
    {
        /// <summary>
        /// Relationship methods.
        /// </summary>
        public const string HasOne = "HasOne";
        public const string HasMany = "HasMany";
        public const string WithOne = "WithOne";
        public const string WithMany = "WithMany";

        /// <summary>
        /// Configuration methods.
        /// </summary>
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
        public const string OwnsOne = "OwnsOne";
        public const string OwnsMany = "OwnsMany";
        public const string WithOwner = "WithOwner";

        /// <summary>
        /// DbContext methods.
        /// </summary>
        public const string OnModelCreating = "OnModelCreating";
        public const string BuildModel = "BuildModel";

        /// <summary>
        /// Entity type configuration (<c>IEntityTypeConfiguration&lt;T&gt;</c>) methods and names.
        /// </summary>
        public const string ApplyConfiguration = "ApplyConfiguration";
        public const string ApplyConfigurationsFromAssembly = "ApplyConfigurationsFromAssembly";
        public const string Configure = "Configure";
        public const string EntityTypeConfigurationInterface = "IEntityTypeConfiguration";
    }

    /// <summary>
    /// Contains constant values for Entity Framework attribute names.
    /// </summary>
    internal static class EfAttributes
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
    internal static class CommonNames
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
    internal static class CollectionTypes
    {
        private const string ICollection = "ICollection";
        private const string IList = "IList";
        private const string List = "List";
        private const string HashSet = "HashSet";
        private const string ISet = "ISet";
        private const string IEnumerable = "IEnumerable";

        /// <summary>
        /// A set of common collection types.
        /// </summary>
        public static readonly IReadOnlySet<string> SupportedCollections =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ICollection,
                IList,
                List,
                HashSet,
                ISet,
                IEnumerable
            };
    }

    /// <summary>
    /// Contains constant values for string suffixes and patterns.
    /// </summary>
    internal static class Suffixes
    {
        public const string IdSuffix = "Id";
    }

    /// <summary>
    /// Contains constant values for relationship type keys and delimiters.
    /// </summary>
    internal static class RelationshipKeys
    {
        public const string Delimiter = "-";
    }

    /// <summary>
    /// Contains constant values for file patterns and extensions.
    /// </summary>
    internal static class FilePatterns
    {
        public const string CSharpFiles = FilePathGuard.CSharpFilesPattern;
    }

    /// <summary>
    /// Contains constant values for SQL type patterns and formats.
    /// </summary>
    internal static class SqlTypePatterns
    {
        public const string DecimalPattern = @"decimal\((\d+),\s*(\d+)\)";
    }

    /// <summary>
    /// Contains constant values for regex patterns used when parsing SQL type strings.
    /// </summary>
    internal static class FluentApiPatterns
    {
        public const string NumericArgumentPattern = @"\((\d+)\)";
    }
}
