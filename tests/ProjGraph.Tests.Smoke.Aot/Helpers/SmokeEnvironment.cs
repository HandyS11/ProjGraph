namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// The smoke suite's configuration: the four builds under comparison, read from environment
/// variables, and the repository root that every command runs from.
/// </summary>
internal static class SmokeEnvironment
{
    private const string CliNativeVariable = "PROJGRAPH_SMOKE_CLI_NATIVE";
    private const string CliReferenceVariable = "PROJGRAPH_SMOKE_CLI_REFERENCE";
    private const string McpNativeVariable = "PROJGRAPH_SMOKE_MCP_NATIVE";
    private const string McpReferenceVariable = "PROJGRAPH_SMOKE_MCP_REFERENCE";
    private const string RequiredVariable = "PROJGRAPH_SMOKE_REQUIRED";

    private static readonly string[] RequiredVariables =
        [CliNativeVariable, CliReferenceVariable, McpNativeVariable, McpReferenceVariable];

    private static readonly Lazy<string> LazyRepositoryRoot = new(FindRepositoryRoot);

    /// <summary>
    /// Gets the repository root: the nearest ancestor of the test output directory that contains
    /// <c>ProjGraph.slnx</c>.
    /// </summary>
    public static string RepositoryRoot => LazyRepositoryRoot.Value;

    /// <summary>
    /// Gets a value indicating whether the suite must run. CI sets <c>PROJGRAPH_SMOKE_REQUIRED=true</c>,
    /// so a misspelled or missing variable fails the job instead of skipping every test.
    /// </summary>
    public static bool IsRequired =>
        string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the native CLI executable.</summary>
    public static SmokeCommand CliNative => Resolve(CliNativeVariable);

    /// <summary>Gets the JIT CLI build of the same commit.</summary>
    public static SmokeCommand CliReference => Resolve(CliReferenceVariable);

    /// <summary>Gets the native MCP server executable.</summary>
    public static SmokeCommand McpNative => Resolve(McpNativeVariable);

    /// <summary>Gets the JIT MCP server build of the same commit.</summary>
    public static SmokeCommand McpReference => Resolve(McpReferenceVariable);

    /// <summary>
    /// Lists the required variables that are unset or empty.
    /// </summary>
    /// <returns>The missing variable names, empty when the suite is fully configured.</returns>
    public static IReadOnlyList<string> GetMissingVariables()
    {
        return
        [
            .. RequiredVariables.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
        ];
    }

    /// <summary>
    /// Resolves an absolute path under the repository root.
    /// </summary>
    /// <param name="relativePath">A path relative to the repository root, using <c>/</c> separators.</param>
    /// <returns>The absolute path.</returns>
    public static string GetRootPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(RepositoryRoot, relativePath));
    }

    private static SmokeCommand Resolve(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{variable} is not set.");
        }

        // A configured path that does not exist is a broken CI setup, never a reason to skip.
        var path = Path.GetFullPath(value, RepositoryRoot);
        return File.Exists(path)
            ? SmokeCommand.For(path)
            : throw new FileNotFoundException($"{variable} points to a file that does not exist.", path);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ProjGraph.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No ProjGraph.slnx found above {AppContext.BaseDirectory}.");
    }
}
