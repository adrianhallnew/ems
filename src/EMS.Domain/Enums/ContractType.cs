namespace EMS.Domain.Enums;

/// <summary>
/// Employment contract an employee is engaged under.
/// </summary>
public enum ContractType
{
    /// <summary>Full-time employment.</summary>
    FullTime,

    /// <summary>Part-time employment.</summary>
    PartTime,

    /// <summary>Fixed-term contract.</summary>
    Contract,

    /// <summary>Internship.</summary>
    Intern,
}
