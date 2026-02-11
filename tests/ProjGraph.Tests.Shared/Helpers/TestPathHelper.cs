namespace ProjGraph.Tests.Shared.Helpers;

/// <summary>
/// Provides shared path resolution helpers for test projects.
/// </summary>
public static class TestPathHelper
{
    /// <summary>
    /// Resolves a relative path under the <c>samples/</c> directory to a full path.
    /// </summary>
    /// <param name="relativePath">The path relative to the samples directory (supports both / and \ separators).</param>
    /// <returns>The full resolved path.</returns>
    public static string GetSamplePath(string relativePath)
    {
        var parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var pathParts = new[] { Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "samples" }
            .Concat(parts)
            .ToArray();
        return Path.GetFullPath(Path.Combine(pathParts));
    }

    /// <summary>
    /// Resolves a relative path from the repository root to a full path.
    /// </summary>
    /// <param name="relativePath">The path relative to the repository root.</param>
    /// <returns>The full resolved path.</returns>
    public static string GetRootPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            relativePath));
    }
}
