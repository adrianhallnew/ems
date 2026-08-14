namespace EMS.Domain.Exceptions;

/// <summary>
/// Base type for violations of a domain invariant.
/// </summary>
/// <remarks>
/// These signal bugs, not outcomes. Expected failures — an overlapping leave request, an
/// insufficient balance, a second clock-in on the same day — are returned as results from the
/// Application layer and never surface as exceptions.
/// </remarks>
public abstract class DomainException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="DomainException"/> class.</summary>
    protected DomainException()
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DomainException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    protected DomainException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DomainException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
