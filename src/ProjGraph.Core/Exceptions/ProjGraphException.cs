namespace ProjGraph.Core.Exceptions;

/// <summary>
/// Base exception for all ProjGraph-specific errors.
/// Allows consumers to distinguish ProjGraph errors from framework exceptions.
/// </summary>
public class ProjGraphException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjGraphException"/> class.
    /// </summary>
    public ProjGraphException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjGraphException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ProjGraphException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjGraphException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ProjGraphException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
