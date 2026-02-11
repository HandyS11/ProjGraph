namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Provides common file path validation methods.
/// </summary>
public static class FilePathGuard
{
    /// <summary>
    /// The <c>.cs</c> file extension.
    /// </summary>
    public const string CSharpExtension = ".cs";

    /// <summary>
    /// The glob pattern for C# source files (<c>*.cs</c>).
    /// </summary>
    public const string CSharpFilesPattern = "*" + CSharpExtension;

    /// <summary>
    /// Validates that the given path has a <c>.cs</c> file extension (case-insensitive).
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <param name="paramName">The parameter name to include in the exception.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> does not end with <c>.cs</c>.</exception>
    public static void RequireCsFile(string path, string paramName = "path")
    {
        if (!path.EndsWith(CSharpExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Only .cs files are supported. Got: {path}", paramName);
        }
    }
}
