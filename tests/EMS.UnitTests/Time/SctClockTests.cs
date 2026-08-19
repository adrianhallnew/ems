using EMS.Application.Common.Options;
using EMS.Application.Common.Time;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace EMS.UnitTests.Time;

/// <summary>
/// Covers the day boundary. Seychelles runs at UTC+4, so every instant from 20:00 UTC onward
/// already belongs to the next SCT day — the exact case that reading the UTC date gets wrong.
/// </summary>
public class SctClockTests
{
    private static SctClock ClockAt(DateTimeOffset utcNow)
    {
        var time = new FakeTimeProvider(utcNow);

        return new SctClock(time, Options.Create(new AppSettings()));
    }

    [Fact]
    public void Now_IsFourHoursAheadOfUtc()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero));

        clock.Now.Offset.ShouldBe(TimeSpan.FromHours(4));
        clock.Now.Hour.ShouldBe(10);
    }

    [Theory]
    [InlineData(19, 59, 2026, 8, 19)]   // still the same SCT day
    [InlineData(20, 0, 2026, 8, 20)]    // the boundary: 20:00 UTC is midnight SCT
    [InlineData(23, 59, 2026, 8, 20)]   // the case a UTC date gets wrong
    public void Today_RollsOverAtTwentyHundredUtc(
        int utcHour,
        int utcMinute,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var clock = ClockAt(new DateTimeOffset(2026, 8, 19, utcHour, utcMinute, 0, TimeSpan.Zero));

        clock.Today.ShouldBe(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    [Fact]
    public void DateOf_AssignsALateEveningUtcInstantToTheNextSctDay()
    {
        var clock = ClockAt(DateTimeOffset.UnixEpoch);

        var instant = new DateTime(2026, 8, 19, 22, 30, 0, DateTimeKind.Utc);

        clock.DateOf(instant).ShouldBe(new DateOnly(2026, 8, 20));
    }

    [Fact]
    public void TimeOf_ReturnsTheSeychellesWallClockTime()
    {
        var clock = ClockAt(DateTimeOffset.UnixEpoch);

        var instant = new DateTime(2026, 8, 19, 4, 15, 0, DateTimeKind.Utc);

        clock.TimeOf(instant).ShouldBe(new TimeOnly(8, 15));
    }

    [Fact]
    public void Today_TracksTheProviderRatherThanTheMachine()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));
        var clock = new SctClock(time, Options.Create(new AppSettings()));

        clock.Today.ShouldBe(new DateOnly(2026, 8, 19));

        time.Advance(TimeSpan.FromDays(1));

        clock.Today.ShouldBe(new DateOnly(2026, 8, 20));
    }

    [Fact]
    public void Offset_ComesFromConfigurationRatherThanAConstant()
    {
        var settings = new AppSettings { TimeZoneOffsetHours = 0 };
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 22, 30, 0, TimeSpan.Zero));

        var clock = new SctClock(time, Options.Create(settings));

        clock.Today.ShouldBe(new DateOnly(2026, 8, 19));
    }
}
