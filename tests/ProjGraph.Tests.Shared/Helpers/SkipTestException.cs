namespace ProjGraph.Tests.Shared.Helpers;

/// <summary>
/// Exception that triggers xUnit v2's dynamic test skip mechanism.
/// When thrown during test execution, the test is reported as "skipped" rather than "failed".
/// Compatible with xUnit 2.8+ runners that recognize the <c>$XunitDynamicSkip$</c> message prefix.
/// </summary>
public sealed class SkipTestException(string reason) : Exception($"$XunitDynamicSkip${reason}");
