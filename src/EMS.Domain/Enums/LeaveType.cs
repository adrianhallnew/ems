namespace EMS.Domain.Enums;

/// <summary>
/// Category of leave a request draws against.
/// </summary>
public enum LeaveType
{
    /// <summary>Standard annual leave.</summary>
    Annual,

    /// <summary>Medical leave.</summary>
    Sick,

    /// <summary>Maternity leave. Granted explicitly by an Admin; never auto-created.</summary>
    Maternity,

    /// <summary>Unpaid leave. Uncapped, and deducted from no balance.</summary>
    Unpaid,

    /// <summary>Bereavement and family emergencies.</summary>
    Compassionate,
}
