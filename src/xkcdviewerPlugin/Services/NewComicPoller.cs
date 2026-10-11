namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // Asks xkcd for the latest comic once when the plugin loads, then at the times ReleaseSchedule gives: hourly on
    // xkcd's release days, in the US afternoon, at this installation's own minute, until that day's comic is out. A
    // failed check isn't retried; the next slot is the next try. What a check finds is handled by whoever supplies
    // `check` (the comic service, which tells the NewComicTracker).
    //
    // The wait for the next slot runs in steps of at most waitStep, re-reading the clock each time, because a timer
    // doesn't count while the computer sleeps. A slot missed while asleep is skipped, not caught up.
    public sealed class NewComicPoller : IDisposable
    {
        private readonly Func<Task<xkcdComic>> _check;
        private readonly IPluginLog _log;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _offsetInHour;
        private readonly TimeZoneInfo _zone;
        private readonly TimeSpan _waitStep;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();

        private Task _running;

        // check returns the latest comic, or null if asking failed.
        public NewComicPoller(Func<Task<xkcdComic>> check, IPluginLog log, TimeSpan offsetInHour)
            : this(check, log, offsetInHour, TimeProvider.System, ReleaseSchedule.Eastern, PluginTimings.NewComicCheckWaitStep, null)
        {
        }

        // delay null waits on timeProvider; tests pass one that moves a manual clock instead.
        internal NewComicPoller(
            Func<Task<xkcdComic>> check,
            IPluginLog log,
            TimeSpan offsetInHour,
            TimeProvider timeProvider,
            TimeZoneInfo zone,
            TimeSpan waitStep,
            Func<TimeSpan, CancellationToken, Task> delay)
        {
            this._check = check ?? throw new ArgumentNullException(nameof(check));
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this._zone = zone ?? throw new ArgumentNullException(nameof(zone));
            if (offsetInHour < TimeSpan.Zero || offsetInHour >= TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(offsetInHour), offsetInHour, "Must be within the hour");
            }

            if (waitStep <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(waitStep), waitStep, "Must be positive");
            }

            this._offsetInHour = offsetInHour;
            this._waitStep = waitStep;
            this._delay = delay ?? ((wait, token) => Task.Delay(wait, timeProvider, token));
        }

        public void Start()
        {
            this._running ??= Task.Run(this.RunAsync);
        }

        // Ends the loop; a check already under way finishes on its own.
        public void Dispose()
        {
            this._stop.Cancel();
            this._stop.Dispose();
        }

        // The loop itself, for tests to await; it ends when the poller is disposed.
        internal Task RunAsync() => this.RunLoopAsync(this._stop.Token);

        private async Task RunLoopAsync(CancellationToken token)
        {
            try
            {
                var latest = await this.CheckAsync();
                while (!token.IsCancellationRequested)
                {
                    var next = ReleaseSchedule.NextCheck(this._timeProvider.GetUtcNow(), this._offsetInHour, this._zone, latest?.Published);
                    this._log.Info($"Next check for a new comic at {next:u}");

                    for (var now = this._timeProvider.GetUtcNow(); now < next; now = this._timeProvider.GetUtcNow())
                    {
                        var wait = next - now;
                        await this._delay(wait < this._waitStep ? wait : this._waitStep, token);
                    }

                    // A failed check keeps what the last good one knew, so a known "today's comic is out" still stands.
                    latest = await this.CheckAsync() ?? latest;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                this._log.Error($"Checking for new comics stopped: {ex.Message}");
            }
        }

        private async Task<xkcdComic> CheckAsync()
        {
            try
            {
                var latest = await this._check();
                if (latest == null)
                {
                    this._log.Warning("Checking for a new comic failed; trying again at the next slot");
                }

                return latest;
            }
            catch (Exception ex)
            {
                this._log.Error($"Checking for a new comic failed: {ex.Message}");
                return null;
            }
        }
    }
}
