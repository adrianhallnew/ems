using System.Globalization;
using EMS.Domain.Enums;

namespace EMS.Application.Notifications;

/// <summary>
/// The notification texts, in one place.
/// </summary>
/// <remarks>
/// Spec §3.9.1 states each message verbatim. Keeping them here rather than inline in five services
/// means the wording is checkable against that table by a test, and a change lands once.
/// </remarks>
public static class NotificationMessages
{
    /// <summary>Title for a submitted request.</summary>
    public const string LeaveSubmittedTitle = "Leave request submitted";

    /// <summary>Title for an approval.</summary>
    public const string LeaveApprovedTitle = "Leave approved";

    /// <summary>Title for a rejection.</summary>
    public const string LeaveRejectedTitle = "Leave rejected";

    /// <summary>Title for any cancellation.</summary>
    public const string LeaveCancelledTitle = "Leave cancelled";

    /// <summary>Title for a flagged missing clock-out.</summary>
    public const string MissedClockOutTitle = "Missed clock-out";

    /// <summary>Title for a department left without a manager.</summary>
    public const string ManagerVacatedTitle = "Department manager vacated";

    /// <summary>Title for an Admin balance change.</summary>
    public const string BalanceAdjustedTitle = "Leave balance adjusted";

    /// <summary>"{Employee Name} has submitted a {Leave Type} leave request for {dates}".</summary>
    public static string LeaveSubmitted(string employeeName, LeaveType leaveType, string dates) =>
        $"{employeeName} has submitted a {leaveType} leave request for {dates}";

    /// <summary>"Your {Leave Type} leave request for {dates} has been approved".</summary>
    public static string LeaveApproved(LeaveType leaveType, string dates) =>
        $"Your {leaveType} leave request for {dates} has been approved";

    /// <summary>
    /// "Your {Leave Type} leave request for {dates} has been rejected. Reason: {note}".
    /// </summary>
    /// <param name="leaveType">The type requested.</param>
    /// <param name="dates">The formatted range.</param>
    /// <param name="note">The reviewer's note, which spec §3.4.3 leaves optional.</param>
    /// <returns>The message.</returns>
    public static string LeaveRejected(LeaveType leaveType, string dates, string? note) =>
        $"Your {leaveType} leave request for {dates} has been rejected. Reason: {(string.IsNullOrWhiteSpace(note) ? "none given" : note)}";

    /// <summary>"{Employee Name} has cancelled their {Leave Type} leave for {dates}".</summary>
    public static string LeaveCancelledByEmployee(
        string employeeName,
        LeaveType leaveType,
        string dates) =>
        $"{employeeName} has cancelled their {leaveType} leave for {dates}";

    /// <summary>
    /// "Your {Leave Type} leave for {dates} has been cancelled by an administrator".
    /// </summary>
    public static string LeaveCancelledByAdmin(LeaveType leaveType, string dates) =>
        $"Your {leaveType} leave for {dates} has been cancelled by an administrator";

    /// <summary>"{Employee Name} did not clock out on {date}".</summary>
    public static string MissedClockOut(string employeeName, DateOnly date) =>
        $"{employeeName} did not clock out on {Format(date)}";

    /// <summary>"{Department} no longer has an assigned manager".</summary>
    public static string ManagerVacated(string departmentName) =>
        $"{departmentName} no longer has an assigned manager";

    /// <summary>"Your {Leave Type} balance has been adjusted by an administrator".</summary>
    public static string BalanceAdjusted(LeaveType leaveType) =>
        $"Your {leaveType} balance has been adjusted by an administrator";

    /// <summary>Formats a leave range, collapsing a single day to one date.</summary>
    /// <param name="startDate">The first day.</param>
    /// <param name="endDate">The last day.</param>
    /// <returns>The range as it appears in a message.</returns>
    public static string DateRange(DateOnly startDate, DateOnly endDate) =>
        startDate == endDate
            ? Format(startDate)
            : $"{Format(startDate)} to {Format(endDate)}";

    private static string Format(DateOnly date) =>
        date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
