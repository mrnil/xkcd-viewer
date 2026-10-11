namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Xunit;

    // Dates used: Friday 9, Monday 12, Wednesday 14 and Friday 16 October 2026 are in US daylight time (UTC-4);
    // Monday 2 November 2026 is the first release day after the change back to standard time (UTC-5, on 1 November).
    public class ReleaseScheduleTests
    {
        private static readonly TimeZoneInfo _eastern = ReleaseSchedule.Eastern;
        private static readonly TimeSpan _offset = TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(45);

        [Fact]
        public void Eastern_IsUsEasternTime_WithDaylightSaving()
        {
            Assert.Equal(TimeSpan.FromHours(-4), _eastern.GetUtcOffset(new DateTime(2026, 7, 1, 12, 0, 0)));
            Assert.Equal(TimeSpan.FromHours(-5), _eastern.GetUtcOffset(new DateTime(2026, 12, 1, 12, 0, 0)));
        }

        [Theory]
        [InlineData(DayOfWeek.Monday, true)]
        [InlineData(DayOfWeek.Tuesday, false)]
        [InlineData(DayOfWeek.Wednesday, true)]
        [InlineData(DayOfWeek.Thursday, false)]
        [InlineData(DayOfWeek.Friday, true)]
        [InlineData(DayOfWeek.Saturday, false)]
        [InlineData(DayOfWeek.Sunday, false)]
        public void IsReleaseDay_IsMondayWednesdayAndFriday(DayOfWeek day, Boolean expected)
        {
            Assert.Equal(expected, ReleaseSchedule.IsReleaseDay(day));
        }

        [Fact]
        public void DayIn_IsTheDayInThatZone_NotInUtc()
        {
            // 02:00 UTC on Tuesday is still Monday evening in New York.
            Assert.Equal(new DateOnly(2026, 10, 12), ReleaseSchedule.DayIn(Utc("2026-10-13T02:00:00Z"), _eastern));
        }

        [Fact]
        public void NextCheck_ReleaseDayMorning_IsTheNoonHourAtTheOffset()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-12T14:00:00Z"), _offset, _eastern, null);   // Monday 10:00

            Assert.Equal(Utc("2026-10-12T16:23:45Z"), next);                                                // Monday 12:23:45
        }

        [Fact]
        public void NextCheck_DuringTheWindow_IsTheNextHourAtTheOffset()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-12T17:30:00Z"), _offset, _eastern, null);   // Monday 13:30

            Assert.Equal(Utc("2026-10-12T18:23:45Z"), next);                                                // Monday 14:23:45
        }

        [Fact]
        public void NextCheck_ExactlyAtACheck_IsTheOneAfter()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-12T16:23:45Z"), _offset, _eastern, null);

            Assert.Equal(Utc("2026-10-12T17:23:45Z"), next);
        }

        [Fact]
        public void NextCheck_TheLastCheckIsInThe22Hour()
        {
            var last = ReleaseSchedule.NextCheck(Utc("2026-10-13T02:00:00Z"), _offset, _eastern, null);   // Monday 22:00

            Assert.Equal(Utc("2026-10-13T02:23:45Z"), last);                                                // Monday 22:23:45
        }

        [Fact]
        public void NextCheck_AfterTheWindow_IsTheNextReleaseDay()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-13T02:30:00Z"), _offset, _eastern, null);   // Monday 22:30

            Assert.Equal(Utc("2026-10-14T16:23:45Z"), next);                                                // Wednesday 12:23:45
        }

        [Fact]
        public void NextCheck_OnAReleaseDayWhoseComicIsOut_SkipsToTheNextReleaseDay()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-12T18:00:00Z"), _offset, _eastern, new DateOnly(2026, 10, 12));

            Assert.Equal(Utc("2026-10-14T16:23:45Z"), next);
        }

        [Fact]
        public void NextCheck_WithAnOlderComicKnown_KeepsCheckingToday()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-12T18:00:00Z"), _offset, _eastern, new DateOnly(2026, 10, 9));

            Assert.Equal(Utc("2026-10-12T18:23:45Z"), next);
        }

        [Fact]
        public void NextCheck_FridayEvening_IsMonday()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-17T03:00:00Z"), _offset, _eastern, null);   // Friday 23:00

            Assert.Equal(Utc("2026-10-19T16:23:45Z"), next);                                                // Monday 12:23:45
        }

        [Fact]
        public void NextCheck_AfterTheClocksGoBack_FollowsStandardTime()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-31T12:00:00Z"), _offset, _eastern, null);   // Saturday

            Assert.Equal(Utc("2026-11-02T17:23:45Z"), next);                                                // Monday 12:23:45 EST
        }

        [Fact]
        public void NextCheck_FridaysComicOut_AndNoOtherDayLeftThisWeek_IsMonday()
        {
            var next = ReleaseSchedule.NextCheck(Utc("2026-10-16T17:00:00Z"), _offset, _eastern, new DateOnly(2026, 10, 16));

            Assert.Equal(Utc("2026-10-19T16:23:45Z"), next);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3600)]
        public void NextCheck_OffsetOutsideTheHour_Throws(Int32 seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReleaseSchedule.NextCheck(Utc("2026-10-12T14:00:00Z"), TimeSpan.FromSeconds(seconds), _eastern, null));
        }

        private static DateTimeOffset Utc(String iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);
    }
}
