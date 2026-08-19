namespace EMS.Application.Reports;

/// <summary>Assembles report rows, with the caller's scope applied.</summary>
/// <remarks>
/// Data assembly only. Rendering to PDF or CSV is <see cref="IReportRenderer"/>'s job, and lives in
/// Infrastructure because QuestPDF and CsvHelper are external concerns.
/// </remarks>
public interface IReportDataService
{
    /// <summary>Builds the monthly attendance summary.</summary>
    /// <param name="request">Range and department, subject to the caller's scope.</param>
    /// <param name="ct">Cancels the queries.</param>
    /// <returns>The report model.</returns>
    Task<AttendanceReportModel> GetAttendanceReportAsync(ReportRequest request, CancellationToken ct);

    /// <summary>Builds the leave balances and usage report.</summary>
    /// <param name="request">Range, department and leave type, subject to the caller's scope.</param>
    /// <param name="ct">Cancels the queries.</param>
    /// <returns>The report model.</returns>
    Task<LeaveReportModel> GetLeaveReportAsync(ReportRequest request, CancellationToken ct);

    /// <summary>Builds the department headcount and employee directory.</summary>
    /// <param name="request">Department narrowing, subject to the caller's scope.</param>
    /// <param name="ct">Cancels the queries.</param>
    /// <returns>The report model, which carries no salary for any role.</returns>
    Task<DirectoryReportModel> GetDirectoryReportAsync(ReportRequest request, CancellationToken ct);

    /// <summary>Resolves a preset period into concrete SCT dates.</summary>
    /// <param name="request">The request carrying the preset or the custom range.</param>
    /// <returns>The inclusive range the report covers.</returns>
    (DateOnly From, DateOnly To) ResolveRange(ReportRequest request);
}
