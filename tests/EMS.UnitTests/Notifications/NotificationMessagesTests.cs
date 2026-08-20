using EMS.Application.Notifications;
using EMS.Domain.Enums;
using Shouldly;

namespace EMS.UnitTests.Notifications;

/// <summary>
/// Pins the notification wording against the table in spec §3.9.1. These are the only strings the
/// specification quotes verbatim, so they are worth a test rather than a review.
/// </summary>
public class NotificationMessagesTests
{
    [Fact]
    public void LeaveSubmitted_MatchesTheSpecifiedWording()
    {
        NotificationMessages
            .LeaveSubmitted("Marie Adrienne", LeaveType.Annual, "17 Aug 2026 to 21 Aug 2026")
            .ShouldBe("Marie Adrienne has submitted a Annual leave request for 17 Aug 2026 to 21 Aug 2026");
    }

    [Fact]
    public void LeaveApproved_MatchesTheSpecifiedWording()
    {
        NotificationMessages
            .LeaveApproved(LeaveType.Sick, "17 Aug 2026")
            .ShouldBe("Your Sick leave request for 17 Aug 2026 has been approved");
    }

    [Fact]
    public void LeaveRejected_CarriesTheReviewersReason()
    {
        NotificationMessages
            .LeaveRejected(LeaveType.Annual, "17 Aug 2026", "Cover is short that week")
            .ShouldBe("Your Annual leave request for 17 Aug 2026 has been rejected. Reason: Cover is short that week");
    }

    [Fact]
    public void LeaveRejected_WithNoNote_StillReadsAsASentence()
    {
        // The note is optional (spec §3.4.3), so the message must not end in a dangling colon.
        NotificationMessages
            .LeaveRejected(LeaveType.Annual, "17 Aug 2026", null)
            .ShouldBe("Your Annual leave request for 17 Aug 2026 has been rejected. Reason: none given");
    }

    [Fact]
    public void LeaveCancelledByEmployee_AddressesTheAdmins()
    {
        NotificationMessages
            .LeaveCancelledByEmployee("Marie Adrienne", LeaveType.Compassionate, "17 Aug 2026")
            .ShouldBe("Marie Adrienne has cancelled their Compassionate leave for 17 Aug 2026");
    }

    [Fact]
    public void LeaveCancelledByAdmin_AddressesTheEmployee()
    {
        NotificationMessages
            .LeaveCancelledByAdmin(LeaveType.Annual, "17 Aug 2026")
            .ShouldBe("Your Annual leave for 17 Aug 2026 has been cancelled by an administrator");
    }

    [Fact]
    public void MissedClockOut_NamesTheEmployeeAndTheDate()
    {
        NotificationMessages
            .MissedClockOut("Marie Adrienne", new DateOnly(2026, 8, 19))
            .ShouldBe("Marie Adrienne did not clock out on 19 Aug 2026");
    }

    [Fact]
    public void ManagerVacated_NamesTheDepartment()
    {
        NotificationMessages
            .ManagerVacated("Finance")
            .ShouldBe("Finance no longer has an assigned manager");
    }

    [Fact]
    public void BalanceAdjusted_NamesTheLeaveType()
    {
        NotificationMessages
            .BalanceAdjusted(LeaveType.Maternity)
            .ShouldBe("Your Maternity balance has been adjusted by an administrator");
    }

    [Fact]
    public void DateRange_CollapsesASingleDay()
    {
        NotificationMessages
            .DateRange(new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 17))
            .ShouldBe("17 Aug 2026");
    }

    [Fact]
    public void DateRange_IsCultureIndependent()
    {
        // Formatted with the invariant culture, so a server's locale cannot change a message.
        NotificationMessages
            .DateRange(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 24))
            .ShouldBe("01 Dec 2026 to 24 Dec 2026");
    }
}
