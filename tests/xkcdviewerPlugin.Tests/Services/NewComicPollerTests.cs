namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    using Moq;

    using Xunit;

    // The waits don't sleep: the fake delay moves the manual clock by however long the poller asked to wait. The
    // check stops the loop (by disposing the poller) once it has run the number of times a test needs.
    public class NewComicPollerTests
    {
        private static readonly TimeSpan Offset = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan WaitStep = TimeSpan.FromMinutes(15);
        private static readonly DateOnly Friday = new DateOnly(2026, 10, 9);
        private static readonly DateOnly Monday = new DateOnly(2026, 10, 12);

        private readonly Mock<IPluginLog> _mockLog = new Mock<IPluginLog>();
        private readonly List<TimeSpan> _waits = new List<TimeSpan>();

        // Monday 12 October 2026, 10:00 in New York (UTC-4).
        private readonly ManualTimeProvider _time = new ManualTimeProvider(Utc("2026-10-12T14:00:00Z"));

        [Fact]
        public async Task ChecksAtLoad_ThenHourlyAtTheOffset_FromNoon()
        {
            var checks = await this.RunAsync(4, _ => Comic(3309, Friday));

            Assert.Equal(new[]
            {
                Utc("2026-10-12T14:00:00Z"),    // at load, 10:00
                Utc("2026-10-12T16:15:00Z"),    // 12:15
                Utc("2026-10-12T17:15:00Z"),    // 13:15
                Utc("2026-10-12T18:15:00Z"),    // 14:15
            }, checks);
        }

        [Fact]
        public async Task OnceTodaysComicIsFound_TheNextCheckIsTheNextReleaseDay()
        {
            var checks = await this.RunAsync(4, n => n < 2 ? Comic(3309, Friday) : Comic(3310, Monday));

            Assert.Equal(Utc("2026-10-12T17:15:00Z"), checks[2]);   // Monday 13:15 finds Monday's comic
            Assert.Equal(Utc("2026-10-14T16:15:00Z"), checks[3]);   // so the next is Wednesday 12:15
        }

        [Fact]
        public async Task TodaysComicAlreadyOutAtLoad_NoChecksToday()
        {
            var checks = await this.RunAsync(2, _ => Comic(3310, Monday));

            Assert.Equal(Utc("2026-10-14T16:15:00Z"), checks[1]);
        }

        [Fact]
        public async Task AFailedCheck_IsNotRetried_TheNextSlotIsTheNextTry()
        {
            var checks = await this.RunAsync(3, n => n == 1 ? null : Comic(3309, Friday));

            Assert.Equal(Utc("2026-10-12T16:15:00Z"), checks[1]);   // fails
            Assert.Equal(Utc("2026-10-12T17:15:00Z"), checks[2]);   // an hour later, not sooner
            this._mockLog.Verify(x => x.Warning(It.Is<String>(text => text.Contains("failed"))), Times.Once());
        }

        [Fact]
        public async Task AFailedCheck_KeepsWhatTheLastGoodOneKnew()
        {
            var checks = await this.RunAsync(3, n => n == 0 ? Comic(3310, Monday) : null);

            // Monday's comic was found at load; Wednesday's check fails; Monday stays skipped, Wednesday continues.
            Assert.Equal(Utc("2026-10-14T16:15:00Z"), checks[1]);
            Assert.Equal(Utc("2026-10-14T17:15:00Z"), checks[2]);
        }

        [Fact]
        public async Task ACheckThatThrows_IsLogged_AndPollingCarriesOn()
        {
            var checks = await this.RunAsync(2, n => n == 0 ? throw new InvalidOperationException("boom") : Comic(3309, Friday));

            Assert.Equal(2, checks.Count);
            this._mockLog.Verify(x => x.Error(It.Is<String>(text => text.Contains("boom"))), Times.Once());
        }

        [Fact]
        public async Task Waits_AreNeverLongerThanTheWaitStep()
        {
            await this.RunAsync(2, _ => Comic(3310, Monday));   // waits from Monday 10:00 to Wednesday 12:15

            Assert.NotEmpty(this._waits);
            Assert.All(this._waits, wait => Assert.True(wait <= WaitStep, $"waited {wait}"));
        }

        [Fact]
        public void Constructor_BadArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new NewComicPoller(null, this._mockLog.Object, Offset));
            Assert.Throws<ArgumentNullException>(() => new NewComicPoller(() => Task.FromResult<xkcdComic>(null), null, Offset));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NewComicPoller(() => Task.FromResult<xkcdComic>(null), this._mockLog.Object, TimeSpan.FromHours(1)));
        }

        private static xkcdComic Comic(Int32 num, DateOnly published) => new xkcdComic(num, $"Comic {num}", null, published);

        private static DateTimeOffset Utc(String iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

        // Runs the poller until it has checked `count` times; result(n) is what the nth check (from 0) returns, or
        // throws. Returns when each check happened.
        private async Task<List<DateTimeOffset>> RunAsync(Int32 count, Func<Int32, xkcdComic> result)
        {
            var checks = new List<DateTimeOffset>();
            NewComicPoller poller = null;
            poller = new NewComicPoller(
                () =>
                {
                    var n = checks.Count;
                    checks.Add(this._time.GetUtcNow());
                    if (checks.Count == count)
                    {
                        poller.Dispose();
                    }

                    return Task.FromResult(result(n));
                },
                this._mockLog.Object,
                Offset,
                this._time,
                ReleaseSchedule.Eastern,
                WaitStep,
                (wait, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    this._waits.Add(wait);
                    this._time.Advance(wait);
                    return Task.CompletedTask;
                });

            await poller.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
            return checks;
        }
    }
}
