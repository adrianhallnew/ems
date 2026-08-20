using System.Text;
using EMS.Application.Reports;
using EMS.Infrastructure.Reporting;
using Shouldly;

namespace EMS.UnitTests.Reports;

/// <summary>
/// Covers CSV export safety. Without escaping, a leave reason or job title beginning with
/// <c>=</c> becomes a formula that a spreadsheet executes when an Admin opens the export
/// (spec §3.6.5). CsvHelper's default is no escaping at all, so this is a behaviour worth pinning.
/// </summary>
public class ReportRendererTests
{
    [Fact]
    public async Task RenderAttendanceCsvAsync_NeutralisesALeadingEquals()
    {
        var csv = await RenderAsync(EmployeeNamed("=cmd|'/c calc'!A1"));

        csv.ShouldNotContain("\n=cmd");
        csv.ShouldContain("cmd|'/c calc'!A1");
    }

    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData("+1234")]
    [InlineData("-1234")]
    [InlineData("@import")]
    public async Task RenderAttendanceCsvAsync_NeutralisesEveryInjectionCharacter(string dangerous)
    {
        var csv = await RenderAsync(EmployeeNamed(dangerous));

        // Escaped, not stripped: the value stays readable, it just is not a formula any more.
        csv.ShouldContain(dangerous.TrimStart('=', '+', '-', '@'));
        csv.Split('\n').ShouldAllBe(line => !line.StartsWith(dangerous, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenderAttendanceCsvAsync_LeavesOrdinaryValuesAlone()
    {
        var csv = await RenderAsync(EmployeeNamed("Marie Adrienne"));

        csv.ShouldContain("Marie Adrienne");
    }

    [Fact]
    public async Task RenderAttendanceCsvAsync_WritesAHeaderRow()
    {
        var csv = await RenderAsync(EmployeeNamed("Marie Adrienne"));

        csv.ShouldContain(nameof(AttendanceReportRow.DaysPresent));
    }

    [Fact]
    public async Task RenderAttendanceCsvAsync_LeavesTheStreamOpenForTheCaller()
    {
        using var stream = new MemoryStream();

        await new ReportRenderer()
            .RenderAttendanceCsvAsync(Model(EmployeeNamed("Marie Adrienne")), stream, TestContext.Current.CancellationToken);

        // The delivery path rewinds and sends it, so a renderer that disposed it would break
        // downloads (architecture.md §4.11).
        stream.CanRead.ShouldBeTrue();
        stream.Length.ShouldBeGreaterThan(0);
    }

    private static async Task<string> RenderAsync(AttendanceReportRow row)
    {
        using var stream = new MemoryStream();

        await new ReportRenderer()
            .RenderAttendanceCsvAsync(Model(row), stream, TestContext.Current.CancellationToken);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static AttendanceReportModel Model(AttendanceReportRow row) =>
        new(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            "Finance",
            new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            [row]);

    private static AttendanceReportRow EmployeeNamed(string name) =>
        new(name, "Finance", 18, 2, 1, 0, 1, 8_640, 480, 0, 0);
}
