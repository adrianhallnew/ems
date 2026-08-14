namespace EMS.Domain.Exceptions;

/// <summary>
/// Thrown when a date range is incoherent — an end before its start, or a date outside the
/// window the operation can answer for.
/// </summary>
public sealed class InvalidDateRangeException : DomainException
{
    /// <summary>Initialises a new instance of the <see cref="InvalidDateRangeException"/> class.</summary>
    public InvalidDateRangeException()
    {
    }

    /// <summary>Initialises a new instance of the <see cref="InvalidDateRangeException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    public InvalidDateRangeException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="InvalidDateRangeException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public InvalidDateRangeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
