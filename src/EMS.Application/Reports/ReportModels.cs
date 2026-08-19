using EMS.Domain.Enums;

namespace EMS.Application.Reports;

/// <summary>The date-range presets every report offers.</summary>
/// <remarks>Preset boundaries are computed in SCT, consistent with spec section 3.3.3.</remarks>
public enum ReportPeriod
{
    /// <summary>The current SCT month to date.</summary>
    ThisMonth,

    /// <summary>The previous whole SCT month.</summary>
    LastMonth,

    /// <summary>The current SCT year to date.</summary>
    ThisYear,

    /// <summary>The previous whole SCT year.</summary>
    LastYear,

    /// <summary>The caller's own range.</summary>
    Custom,
}

/// <summary>What a report run covers.</summary>
/// <param name="Period">The preset, or Custom.</param>
/// <param name="From">The first date, used when the period is Custom.</param>
/// <param name="To">The last date, used when the period is Custom.</param>
/// <param name="DepartmentId">The department to restrict to, subject to the caller's scope.</param>
/// <param name="LeaveType">The leave type to restrict to, on the leave report.</param>
/// <remarks>
/// Scope is applied server-side when the data is queried. A Manager cannot widen their scope by
/// changing this request (spec section 3.6.4).
/// </remarks>
public sealed record ReportRequest(
    ReportPeriod Period,
    DateOnly? From,
    DateOnly? To,
    Guid? DepartmentId,
    LeaveType? LeaveType);

/// <summary>One employee's attendance totals for the reporting range.</summary>
/// <param name="EmployeeName">The employee's full name.</param>
/// <param name="DepartmentName">The employee's department.</param>
/// <param name="DaysPresent">Days resolved as Present.</param>
/// <param name="DaysLate">Days resolved as Late.</param>
/// <param name="DaysAbsent">Days resolved as Absent.</param>
/// <param name="DaysOnLeave">Days resolved as OnLeave.</param>
/// <param name="Holidays">Days resolved as Holiday.</param>
/// <param name="TotalWorkedMinutes">Minutes worked across the range.</param>
/// <param name="AverageWorkedMinutesPerDay">Mean minutes on days with a recorded pair.</param>
/// <param name="FlaggedRecords">Records the missed-clock-out job flagged.</param>
/// <param name="CorrectedRecords">Records an Admin corrected.</param>
/// <remarks>
/// Minutes, not hours: hours are a formatting decision the renderer makes (ADR-0010).
/// </remarks>
public sealed record AttendanceReportRow(
    string EmployeeName,
    string DepartmentName,
    int DaysPresent,
    int DaysLate,
    int DaysAbsent,
    int DaysOnLeave,
    int Holidays,
    int TotalWorkedMinutes,
    int AverageWorkedMinutesPerDay,
    int FlaggedRecords,
    int CorrectedRecords);

/// <summary>The monthly attendance summary.</summary>
/// <param name="From">The first date covered.</param>
/// <param name="To">The last date covered.</param>
/// <param name="DepartmentName">The department covered, or null for all in scope.</param>
/// <param name="GeneratedAt">When the report was assembled, in UTC.</param>
/// <param name="Rows">One row per employee.</param>
public sealed record AttendanceReportModel(
    DateOnly From,
    DateOnly To,
    string? DepartmentName,
    DateTime GeneratedAt,
    IReadOnlyList<AttendanceReportRow> Rows);

/// <summary>One employee's balance and request counts for one leave type.</summary>
/// <param name="EmployeeName">The employee's full name.</param>
/// <param name="DepartmentName">The employee's department.</param>
/// <param name="LeaveType">The leave type.</param>
/// <param name="Entitlement">Days granted for the current period.</param>
/// <param name="Used">Days consumed.</param>
/// <param name="Remaining">Days still available.</param>
/// <param name="Approved">Approved requests in the range.</param>
/// <param name="Rejected">Rejected requests in the range.</param>
/// <param name="Cancelled">Cancelled requests in the range.</param>
/// <param name="Pending">Requests still awaiting a decision.</param>
public sealed record LeaveReportRow(
    string EmployeeName,
    string DepartmentName,
    LeaveType LeaveType,
    int Entitlement,
    int Used,
    int Remaining,
    int Approved,
    int Rejected,
    int Cancelled,
    int Pending);

/// <summary>The leave balances and usage report.</summary>
/// <param name="From">The first date covered.</param>
/// <param name="To">The last date covered.</param>
/// <param name="DepartmentName">The department covered, or null for all in scope.</param>
/// <param name="GeneratedAt">When the report was assembled, in UTC.</param>
/// <param name="Rows">One row per employee and leave type.</param>
public sealed record LeaveReportModel(
    DateOnly From,
    DateOnly To,
    string? DepartmentName,
    DateTime GeneratedAt,
    IReadOnlyList<LeaveReportRow> Rows);

/// <summary>One employee in the directory.</summary>
/// <param name="FullName">The employee's full name.</param>
/// <param name="JobTitle">The job title.</param>
/// <param name="ContractType">The contract type.</param>
/// <param name="HireDate">The hire date.</param>
/// <param name="Status">Active or Inactive.</param>
/// <remarks>Salary is excluded from this report for every role (spec section 3.6.3).</remarks>
public sealed record DirectoryEmployeeRow(
    string FullName,
    string JobTitle,
    ContractType ContractType,
    DateOnly HireDate,
    EmployeeStatus Status);

/// <summary>One department in the directory.</summary>
/// <param name="DepartmentName">The department name.</param>
/// <param name="ManagerName">The assigned manager, or null.</param>
/// <param name="Headcount">Active employees in the department.</param>
/// <param name="Employees">The employees themselves.</param>
public sealed record DirectoryDepartmentGroup(
    string DepartmentName,
    string? ManagerName,
    int Headcount,
    IReadOnlyList<DirectoryEmployeeRow> Employees);

/// <summary>The department headcount and employee directory.</summary>
/// <param name="GeneratedAt">When the report was assembled, in UTC.</param>
/// <param name="Departments">One group per department in scope.</param>
public sealed record DirectoryReportModel(
    DateTime GeneratedAt,
    IReadOnlyList<DirectoryDepartmentGroup> Departments);
