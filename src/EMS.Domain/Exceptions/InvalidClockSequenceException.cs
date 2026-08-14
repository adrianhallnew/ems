namespace EMS.Domain.Exceptions;

/// <summary>
/// Thrown when an attendance record would carry a clock-out at or before its clock-in.
/// </summary>
/// <remarks>
/// Admin corrections are subject to the same invariant as live clock events.
/// </remarks>
public sealed class InvalidClockSequenceException : DomainException
{
    /// <summary>Initialises a new instance of the <see cref="InvalidClockSequenceException"/> class.</summary>
    public InvalidClockSequenceException()
    {
    }

    /// <summary>Initialises a new instance of the <see cref="InvalidClockSequenceException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    public InvalidClockSequenceException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="InvalidClockSequenceException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public InvalidClockSequenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
