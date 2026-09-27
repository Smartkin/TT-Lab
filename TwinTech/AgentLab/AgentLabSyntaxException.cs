using System;

namespace Twinsanity.AgentLab;

/// <summary>
/// Error in an AgentLab script with the place in the script where it happened
/// </summary>
public class AgentLabSyntaxException : Exception
{
    /// <summary>
    /// Line where the error happened, starting from 1
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Column where the error happened, starting from 1
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Creates the error at the place in the script
    /// </summary>
    public AgentLabSyntaxException(string message, int line, int column, Exception innerException = null) : base(message, innerException)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Message with the error's position in front of it
    /// </summary>
    public override string ToString()
    {
        return $"({Line}:{Column}) {Message}";
    }
}
