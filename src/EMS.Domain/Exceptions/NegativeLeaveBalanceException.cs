namespace EMS.Domain.Exceptions;

/// <summary>
/// Thrown when a leave balance would be driven below zero, or restored above its entitlement.
/// </summary>
/// <remarks>
/// A request that simply exceeds the remaining balance is an ordinary validation outcome, not
/// this. This exception means the guards upstream failed to hold.
/// </remarks>
public sealed class NegativeLeaveBalanceException : DomainException
{
    /// <summary>Initialises a new instance of the <see cref="NegativeLeaveBalanceException"/> class.</summary>
    public NegativeLeaveBalanceException()
    {
    }

    /// <summary>Initialises a new instance of the <see cref="NegativeLeaveBalanceException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    public NegativeLeaveBalanceException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="NegativeLeaveBalanceException"/> class.</summary>
    /// <param name="message">Description of the violated invariant.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public NegativeLeaveBalanceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
