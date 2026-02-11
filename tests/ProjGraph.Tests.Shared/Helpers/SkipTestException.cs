namespace ProjGraph.Tests.Shared.Helpers;

/// <summary>
/// Exception that triggers xUnit v2's dynamic test skip mechanism.
/// When thrown during test execution, the test is reported as "skipped" rather than "failed".
/// Compatible with xUnit 2.8+ runners that recognize the <c>$XunitDynamicSkip$</c> message prefix.
/// </summary>
public sealed class SkipTestException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipTestException"/> class.
    /// </summary>
    public SkipTestException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipTestException"/> class with a skip reason.
    /// </summary>
    /// <param name="reason">The reason for skipping the test.</param>
    public SkipTestException(string reason) : base($"$XunitDynamicSkip${reason}")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipTestException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SkipTestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
