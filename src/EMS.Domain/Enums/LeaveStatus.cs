namespace EMS.Domain.Enums;

/// <summary>
/// State of a leave request. Rejected and Cancelled are terminal.
/// </summary>
public enum LeaveStatus
{
    /// <summary>Awaiting an Admin decision.</summary>
    Pending,

    /// <summary>Approved. The balance has been decremented.</summary>
    Approved,

    /// <summary>Refused by an Admin. Terminal.</summary>
    Rejected,

    /// <summary>Withdrawn by the employee or cancelled by an Admin. Terminal.</summary>
    Cancelled,
}
