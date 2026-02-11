namespace ProjGraph.Core.Exceptions;

/// <summary>
/// Exception thrown when an error occurs while parsing solution, project, or source files.
/// </summary>
public class ParsingException : ProjGraphException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParsingException"/> class.
    /// </summary>
    public ParsingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ParsingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsingException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ParsingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
