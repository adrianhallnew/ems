namespace EMS.Application.Reports;

/// <summary>Renders assembled report models to a stream. Implemented in Infrastructure.</summary>
/// <remarks>
/// Writing to a caller-supplied <see cref="Stream"/> rather than returning a byte array is what
/// lets the delivery path work at all: Blazor Server marshals a byte array over the SignalR
/// connection, whose default maximum message size is 32 KB (ADR-0011).
/// <para>
/// Every CSV writer sets <c>InjectionOptions.Escape</c>. Without it, a leave reason beginning with
/// <c>=</c> becomes a formula that executes when an Admin opens the export.
/// </para>
/// </remarks>
public interface IReportRenderer
{
    /// <summary>Renders the attendance summary as PDF.</summary>
    /// <param name="model">The assembled rows.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">Cancels the render.</param>
    /// <returns>A task that completes when the document is written.</returns>
    Task RenderAttendancePdfAsync(AttendanceReportModel model, Stream output, CancellationToken ct);

    /// <summary>Renders the attendance summary as CSV.</summary>
    /// <param name="model">The assembled rows.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">Cancels the render.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task RenderAttendanceCsvAsync(AttendanceReportModel model, Stream output, CancellationToken ct);

    /// <summary>Renders the leave report as PDF.</summary>
    /// <param name="model">The assembled rows.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">Cancels the render.</param>
    /// <returns>A task that completes when the document is written.</returns>
    Task RenderLeavePdfAsync(LeaveReportModel model, Stream output, CancellationToken ct);

    /// <summary>Renders the leave report as CSV.</summary>
    /// <param name="model">The assembled rows.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">Cancels the render.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task RenderLeaveCsvAsync(LeaveReportModel model, Stream output, CancellationToken ct);

    /// <summary>Renders the directory as PDF.</summary>
    /// <param name="model">The assembled groups.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">Cancels the render.</param>
    /// <returns>A task that completes when the document is written.</returns>
    Task RenderDirectoryPdfAsync(DirectoryReportModel model, Stream output, CancellationToken ct);

    /// <summary>Renders the directory as CSV.</summary>
    /// <param name="model">The assembled groups.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">Cancels the render.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task RenderDirectoryCsvAsync(DirectoryReportModel model, Stream output, CancellationToken ct);
}
