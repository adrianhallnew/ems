using EMS.Domain.Common;
using EMS.Domain.Enums;
using EMS.Domain.Exceptions;

namespace EMS.Domain.Entities;

/// <summary>
/// An employee's business data. Credentials, login email, and role membership are owned by
/// ASP.NET Identity; this entity owns everything else.
/// </summary>
public class Employee : BaseEntity, IAuditableEntity
{
    /// <summary>Gets or sets the Identity user key this employee record belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the given name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Gets or sets the family name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address, which is also the login username.
    /// </summary>
    /// <remarks>
    /// Identity is authoritative for this value. A change updates Identity first, then this
    /// projection, both in one transaction.
    /// </remarks>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the contact telephone number.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Gets or sets the date of birth.</summary>
    public DateOnly DateOfBirth { get; set; }

    /// <summary>Gets or sets the full home address.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Gets or sets the emergency contact's name.</summary>
    public string EmergencyContactName { get; set; } = string.Empty;

    /// <summary>Gets or sets the emergency contact's telephone number.</summary>
    public string EmergencyContactPhone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the monthly salary in SCR. Visible to Admins only, and excluded from any
    /// projection returned to another role.
    /// </summary>
    public decimal Salary { get; set; }

    /// <summary>Gets or sets the job title.</summary>
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the contract the employee is engaged under.</summary>
    public ContractType ContractType { get; set; }

    /// <summary>Gets or sets the department this employee belongs to. Exactly one.</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Gets or sets the department navigation.</summary>
    public Department? Department { get; set; }

    /// <summary>
    /// Gets or sets the role projected from Identity role membership.
    /// </summary>
    /// <remarks>
    /// Maintained on every role change so reports and grids can filter without joining the
    /// Identity tables. Authorisation decisions read the Identity role claim, never this.
    /// </remarks>
    public EmployeeRole Role { get; set; }

    /// <summary>Gets or sets the hire date, which anchors probation and leave balance periods.</summary>
    public DateOnly HireDate { get; set; }

    /// <summary>Gets or sets the lifecycle status. Deletion is always soft.</summary>
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>
    /// Gets or sets the UTC instant of deactivation, or null while active.
    /// </summary>
    /// <remarks>
    /// Attendance state resolution needs to know when an employee left, so that dates after
    /// their departure resolve to NotEmployed rather than counting as absences.
    /// </remarks>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the employee must change their password before
    /// reaching any other page.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Returns whether the employee is still within their probation period.
    /// </summary>
    /// <param name="today">The current SCT calendar date.</param>
    /// <param name="probationMonths">Length of the probation period in months.</param>
    /// <returns><c>true</c> while probation is running; <c>false</c> from the boundary date onward.</returns>
    /// <remarks>
    /// Takes the date as a parameter rather than reading a clock, which keeps Domain free of
    /// dependencies and makes the boundary trivially testable.
    /// </remarks>
    public bool IsInProbation(DateOnly today, int probationMonths) =>
        today < HireDate.AddMonths(probationMonths);

    /// <summary>
    /// Returns the leave balance period containing <paramref name="today"/>.
    /// </summary>
    /// <param name="today">The current SCT calendar date.</param>
    /// <returns>
    /// The period running from the most recent hire anniversary at or before
    /// <paramref name="today"/> to the day before the next one.
    /// </returns>
    /// <exception cref="InvalidDateRangeException">
    /// <paramref name="today"/> precedes the hire date, so no period contains it.
    /// </exception>
    /// <remarks>
    /// A 29 February hire date resolves to 28 February in non-leap years, and periods stay
    /// contiguous across the boundary.
    /// </remarks>
    public (DateOnly Start, DateOnly End) PeriodFor(DateOnly today)
    {
        if (today < HireDate)
        {
            throw new InvalidDateRangeException(
                $"Date {today:O} precedes the hire date {HireDate:O}, so it falls in no balance period.");
        }

        var start = HireDate.AddYears(today.Year - HireDate.Year);
        if (start > today)
        {
            start = HireDate.AddYears(today.Year - HireDate.Year - 1);
        }

        return (start, start.AddYears(1).AddDays(-1));
    }
}
