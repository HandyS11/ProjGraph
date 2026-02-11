namespace ProjGraph.Core.Exceptions;

/// <summary>
/// Exception thrown when an error occurs during code analysis (class diagrams, EF models, etc.).
/// </summary>
public class AnalysisException : ProjGraphException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisException"/> class.
    /// </summary>
    public AnalysisException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AnalysisException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AnalysisException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
