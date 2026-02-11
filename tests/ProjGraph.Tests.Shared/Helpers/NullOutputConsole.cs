using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Shared.Helpers;

/// <summary>
/// A no-op implementation of <see cref="IOutputConsole"/> for use in tests
/// where console output is not relevant to the assertions.
/// </summary>
public sealed class NullOutputConsole : IOutputConsole
{
    public void Write(string message) { }
    public void WriteLine(string message) { }
    public void WriteInfo(string message) { }
    public void WriteError(string message) { }
    public void WriteWarning(string message) { }
    public void WriteSuccess(string message) { }
    public void WriteMarkup(string markup) { }
}
