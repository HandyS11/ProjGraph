namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// How to start one build of a tool: an executable is started directly, and a framework-dependent
/// <c>.dll</c> is started through the <c>dotnet</c> host.
/// </summary>
/// <param name="FileName">The program to start.</param>
/// <param name="LeadingArguments">Arguments that precede the tool's own arguments.</param>
internal sealed record SmokeCommand(string FileName, IReadOnlyList<string> LeadingArguments)
{
    /// <summary>
    /// Creates the command that starts the build at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The absolute path to an executable or a <c>.dll</c>.</param>
    /// <returns>The command.</returns>
    public static SmokeCommand For(string path)
    {
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new SmokeCommand(path, []);
        }

        // dotnet test exports the host that runs the tests, so the reference build runs on the same dotnet host.
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return new SmokeCommand(string.IsNullOrEmpty(host) ? "dotnet" : host, [path]);
    }
}
