using System.Diagnostics;
using System.Text;

namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// The observable result of one CLI invocation.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything written to standard output.</param>
/// <param name="StandardError">Everything written to standard error.</param>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs a build of the CLI from the repository root and captures its output.
/// </summary>
internal static class ProcessRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Runs <paramref name="command"/> with <paramref name="arguments"/> and waits for it to exit.
    /// </summary>
    /// <param name="command">The build to run.</param>
    /// <param name="arguments">The CLI arguments.</param>
    /// <returns>The exit code and the captured output streams.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started.</exception>
    /// <exception cref="TimeoutException">Thrown when the process does not exit in time; it is killed.</exception>
    public static async Task<ProcessResult> RunAsync(SmokeCommand command, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = SmokeEnvironment.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false
        };
        foreach (var argument in command.LeadingArguments.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Could not start {command.FileName}.");
        using var timeout = new CancellationTokenSource(Timeout);

        // Both streams are drained concurrently so a full pipe buffer can never block the child.
        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"'{command.FileName} {string.Join(' ', arguments)}' did not exit within {Timeout}.");
        }

        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }
}
