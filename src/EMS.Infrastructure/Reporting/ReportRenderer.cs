using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using EMS.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EMS.Infrastructure.Reporting;

/// <summary>Renders report models to PDF and CSV, into a caller-supplied stream.</summary>
/// <remarks>
/// Writing to a stream rather than returning a byte array is what makes delivery work at all: a
/// Blazor Server circuit marshals byte arrays over SignalR, whose default message ceiling is 32 KB
/// (architecture.md §4.11).
/// <para>
/// QuestPDF's <c>GeneratePdf</c> is synchronous and CPU-bound, so each PDF is produced on the thread
/// pool rather than on the caller's — which in Blazor Server is the circuit's.
/// </para>
/// </remarks>
public sealed class ReportRenderer : IReportRenderer
{
    /// <summary>
    /// Escape, not strip: an exported cell beginning with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>,
    /// tab, or carriage return is neutralised so a spreadsheet treats it as text, and the value is
    /// still legible (spec §3.6.5). The default is <c>None</c>, so this is not optional.
    /// </summary>
    private static CsvConfiguration CsvSettings => new(CultureInfo.InvariantCulture)
    {
        InjectionOptions = InjectionOptions.Escape,
    };

    /// <inheritdoc/>
    public Task RenderAttendancePdfAsync(
        AttendanceReportModel model,
        Stream output,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderPdfAsync(
            output,
            "Monthly Attendance Summary",
            $"{model.From:dd MMM yyyy} to {model.To:dd MMM yyyy}"
            + (model.DepartmentName is null ? string.Empty : $" · {model.DepartmentName}"),
            model.GeneratedAt,
            ["Employee", "Department", "Present", "Late", "Absent", "On leave", "Holidays", "Hours", "Avg hrs/day", "Flagged", "Corrected"],
            [.. model.Rows.Select(row => new[]
            {
                row.EmployeeName,
                row.DepartmentName,
                row.DaysPresent.ToString(CultureInfo.InvariantCulture),
                row.DaysLate.ToString(CultureInfo.InvariantCulture),
                row.DaysAbsent.ToString(CultureInfo.InvariantCulture),
                row.DaysOnLeave.ToString(CultureInfo.InvariantCulture),
                row.Holidays.ToString(CultureInfo.InvariantCulture),
                Hours(row.TotalWorkedMinutes),
                Hours(row.AverageWorkedMinutesPerDay),
                row.FlaggedRecords.ToString(CultureInfo.InvariantCulture),
                row.CorrectedRecords.ToString(CultureInfo.InvariantCulture),
            })],
            ct);
    }

    /// <inheritdoc/>
    public Task RenderAttendanceCsvAsync(
        AttendanceReportModel model,
        Stream output,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return WriteCsvAsync(output, model.Rows, ct);
    }

    /// <inheritdoc/>
    public Task RenderLeavePdfAsync(LeaveReportModel model, Stream output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderPdfAsync(
            output,
            "Leave Balances & Usage",
            $"{model.From:dd MMM yyyy} to {model.To:dd MMM yyyy}"
            + (model.DepartmentName is null ? string.Empty : $" · {model.DepartmentName}"),
            model.GeneratedAt,
            ["Employee", "Department", "Type", "Entitlement", "Used", "Remaining", "Approved", "Rejected", "Cancelled", "Pending"],
            [.. model.Rows.Select(row => new[]
            {
                row.EmployeeName,
                row.DepartmentName,
                row.LeaveType.ToString(),
                row.Entitlement.ToString(CultureInfo.InvariantCulture),
                row.Used.ToString(CultureInfo.InvariantCulture),
                row.Remaining.ToString(CultureInfo.InvariantCulture),
                row.Approved.ToString(CultureInfo.InvariantCulture),
                row.Rejected.ToString(CultureInfo.InvariantCulture),
                row.Cancelled.ToString(CultureInfo.InvariantCulture),
                row.Pending.ToString(CultureInfo.InvariantCulture),
            })],
            ct);
    }

    /// <inheritdoc/>
    public Task RenderLeaveCsvAsync(LeaveReportModel model, Stream output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return WriteCsvAsync(output, model.Rows, ct);
    }

    /// <inheritdoc/>
    public Task RenderDirectoryPdfAsync(
        DirectoryReportModel model,
        Stream output,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderPdfAsync(
            output,
            "Department Headcount & Employee Directory",
            $"{model.Departments.Sum(d => d.Headcount)} employees in {model.Departments.Count} departments",
            model.GeneratedAt,
            ["Department", "Manager", "Employee", "Job title", "Contract", "Hire date", "Status"],
            [.. model.Departments.SelectMany(department => department.Employees.Select(employee => new[]
            {
                department.DepartmentName,
                department.ManagerName ?? "—",
                employee.FullName,
                employee.JobTitle,
                employee.ContractType.ToString(),
                employee.HireDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                employee.Status.ToString(),
            }))],
            ct);
    }

    /// <inheritdoc/>
    public Task RenderDirectoryCsvAsync(
        DirectoryReportModel model,
        Stream output,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        var rows = model.Departments
            .SelectMany(department => department.Employees.Select(employee => new
            {
                Department = department.DepartmentName,
                Manager = department.ManagerName,
                employee.FullName,
                employee.JobTitle,
                employee.ContractType,
                employee.HireDate,
                employee.Status,
            }))
            .ToList();

        return WriteCsvAsync(output, rows, ct);
    }

    private static async Task WriteCsvAsync<T>(
        Stream output,
        IEnumerable<T> rows,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(output);

        // leaveOpen: the caller owns the stream and may still need to rewind and send it.
        await using var writer = new StreamWriter(output, leaveOpen: true);
        await using var csv = new CsvWriter(writer, CsvSettings, leaveOpen: true);

        await csv.WriteRecordsAsync(rows, ct).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private static Task RenderPdfAsync(
        Stream output,
        string title,
        string subtitle,
        DateTime generatedAt,
        string[] headers,
        IReadOnlyList<string[]> rows,
        CancellationToken ct) =>
        Task.Run(
            () =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(text => text.FontSize(9));

                        page.Header().Column(column =>
                        {
                            column.Item().Text(title).FontSize(16).SemiBold();
                            column.Item().Text(subtitle).FontSize(10).FontColor(Colors.Grey.Darken1);
                        });

                        page.Content().PaddingVertical(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                // The first two columns carry names; the rest are figures.
                                for (var index = 0; index < headers.Length; index++)
                                {
                                    columns.RelativeColumn(index < 2 ? 3 : 1.4f);
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var heading in headers)
                                {
                                    header.Cell()
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Medium)
                                        .PaddingVertical(4)
                                        .Text(heading)
                                        .SemiBold();
                                }
                            });

                            foreach (var row in rows)
                            {
                                foreach (var cell in row)
                                {
                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .PaddingVertical(3)
                                        .Text(cell);
                                }
                            }
                        });

                        page.Footer().Row(row =>
                        {
                            row.RelativeItem()
                                .Text($"Generated {generatedAt:dd MMM yyyy HH:mm} UTC")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);

                            row.ConstantItem(80).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });

                document.GeneratePdf(output);
            },
            ct);

    /// <summary>Minutes as hours, which is how the report presents them (spec §3.6.1).</summary>
    private static string Hours(int minutes) =>
        (minutes / 60.0).ToString("0.0", CultureInfo.InvariantCulture);
}
