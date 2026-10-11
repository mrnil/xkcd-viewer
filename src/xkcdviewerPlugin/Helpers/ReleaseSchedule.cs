namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    // When to ask xkcd whether a new comic is out. xkcd publishes on Monday, Wednesday and Friday, in the US afternoon
    // (see docs/ai/product.md, "Checking for new comics"), so checks run once an hour on those days, from noon to the
    // 22:00 hour, US Eastern time, and stop for the day once that day's comic is out. Each installation checks at its
    // own minute past the hour (offsetInHour, picked at random when the plugin loads), so installations don't all ask
    // xkcd at once. Pure: the clock and the time zone are passed in.
    public static class ReleaseSchedule
    {
        public const Int32 FirstHour = 12;
        public const Int32 LastHour = 22;

        // US Eastern time, following its EST / EDT change. "America/New_York" is the IANA id (.NET resolves it on
        // Windows too); "Eastern Standard Time" is the Windows id, for a system without the IANA mapping.
        public static TimeZoneInfo Eastern { get; } = FindEastern();

        public static Boolean IsReleaseDay(DayOfWeek day) => day is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;

        // The calendar day it is in the given zone at that moment.
        public static DateOnly DayIn(DateTimeOffset moment, TimeZoneInfo zone)
        {
            ArgumentNullException.ThrowIfNull(zone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);
        }

        // The first check strictly after now. latestPublished is the date of the newest comic known (null if none, or
        // undated): on the day it was published there's nothing more to wait for, so that day is skipped.
        public static DateTimeOffset NextCheck(DateTimeOffset now, TimeSpan offsetInHour, TimeZoneInfo zone, DateOnly? latestPublished)
        {
            ArgumentNullException.ThrowIfNull(zone);
            if (offsetInHour < TimeSpan.Zero || offsetInHour >= TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(offsetInHour), offsetInHour, "Must be within the hour");
            }

            var today = DayIn(now, zone);

            // A release day comes round within a week; one more day covers a week whose only remaining day is skipped.
            for (var days = 0; days <= 8; days++)
            {
                var day = today.AddDays(days);
                if (!IsReleaseDay(day.DayOfWeek) || day == latestPublished)
                {
                    continue;
                }

                for (var hour = FirstHour; hour <= LastHour; hour++)
                {
                    // Noon to 23:00 never falls in a daylight-saving change (those happen at 2 a.m.), so this local
                    // time always exists and is never ambiguous.
                    var local = day.ToDateTime(new TimeOnly(hour, 0)) + offsetInHour;
                    var check = new DateTimeOffset(local, zone.GetUtcOffset(local));
                    if (check > now)
                    {
                        return check.ToUniversalTime();
                    }
                }
            }

            throw new InvalidOperationException("No release day found within 9 days");
        }

        private static TimeZoneInfo FindEastern()
        {
            foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
                {
                    return zone;
                }
            }

            // No time zone data at all: a fixed UTC-5 still keeps checks in the US afternoon, an hour out in summer.
            return TimeZoneInfo.CreateCustomTimeZone("Eastern (fixed)", TimeSpan.FromHours(-5), "Eastern (fixed)", "Eastern (fixed)");
        }
    }
}
