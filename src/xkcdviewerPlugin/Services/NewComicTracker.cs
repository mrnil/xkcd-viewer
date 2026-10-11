namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    public enum NewComicLook
    {
        // Nothing unread, or the "off" half of a flash: the key looks as usual.
        Normal,

        // Something is unread: the key is green.
        Green,
    }

    // Whether there's a comic the user hasn't seen, and how the New Comic key and the Comic Browser's Next chevron show
    // it. See docs/ai/product.md, "The new-comic signal".
    //
    // A comic is unread while the newest published (LatestKnown) is newer than the newest shown on the keypad
    // (NewestShown). Both are saved across sessions by the plugin. When a check finds a comic newer than any known
    // before, including at the last run, the keys flash for FlashDuration and then stay green; after a restart with
    // the same comic still unread they're simply green. On a first run the latest comic counts as shown.
    //
    // Thread-safe: comics arrive on background threads and keys are drawn on SDK threads.
    public sealed class NewComicTracker
    {
        private readonly Object _lock = new Object();
        private readonly IPluginLog _log;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _flashDuration;
        private readonly TimeSpan _flashInterval;

        // The latest comic known when the plugin last ran, until this session learns one.
        private readonly Int32? _latestAtLastRun;

        private Int32? _newestShown;
        private Int32? _latestKnown;
        private DateTimeOffset? _flashStartedAt;

        public NewComicTracker(IPluginLog log, Int32? newestShown, Int32? latestAtLastRun)
            : this(log, newestShown, latestAtLastRun, TimeProvider.System, PluginTimings.NewComicFlash, PluginTimings.NewComicFlashInterval)
        {
        }

        internal NewComicTracker(
            IPluginLog log, Int32? newestShown, Int32? latestAtLastRun, TimeProvider timeProvider, TimeSpan flashDuration, TimeSpan flashInterval)
        {
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            if (flashInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(flashInterval), flashInterval, "Must be positive");
            }

            this._flashDuration = flashDuration;
            this._flashInterval = flashInterval;
            this._newestShown = newestShown >= ComicNumbers.First ? newestShown : null;
            this._latestAtLastRun = latestAtLastRun >= ComicNumbers.First ? latestAtLastRun : null;
        }

        // Raised when NewestShown, LatestKnown or the flash changed: save the numbers, redraw the keys. Raised outside
        // the lock, on the thread that made the change.
        public event EventHandler Changed;

        public Int32? NewestShown
        {
            get
            {
                lock (this._lock)
                {
                    return this._newestShown;
                }
            }
        }

        public Int32? LatestKnown
        {
            get
            {
                lock (this._lock)
                {
                    return this._latestKnown ?? this._latestAtLastRun;
                }
            }
        }

        public Boolean HasUnread
        {
            get
            {
                lock (this._lock)
                {
                    return this.HasUnreadLocked();
                }
            }
        }

        // The comic after the newest shown: where the green Next chevron and the New Comic key go. Null with nothing
        // unread.
        public Int32? FirstUnread
        {
            get
            {
                lock (this._lock)
                {
                    return this.HasUnreadLocked() ? this._newestShown + 1 : null;
                }
            }
        }

        // True while the keys are flashing, so the plugin knows to keep redrawing them every FlashInterval.
        public Boolean IsFlashing
        {
            get
            {
                lock (this._lock)
                {
                    return this.IsFlashingLocked(this._timeProvider.GetUtcNow());
                }
            }
        }

        public NewComicLook Look
        {
            get
            {
                lock (this._lock)
                {
                    if (!this.HasUnreadLocked())
                    {
                        return NewComicLook.Normal;
                    }

                    var now = this._timeProvider.GetUtcNow();
                    if (!this.IsFlashingLocked(now))
                    {
                        return NewComicLook.Green;
                    }

                    // Green first, then alternating every interval.
                    var intervals = (Int64)((now - this._flashStartedAt.Value) / this._flashInterval);
                    return intervals % 2 == 0 ? NewComicLook.Green : NewComicLook.Normal;
                }
            }
        }

        // Call whenever xkcd says which comic is the latest, by any route (the hourly check, the check at load, or the
        // user asking for the latest).
        public void OnLatestKnown(Int32 latest)
        {
            if (latest < ComicNumbers.First)
            {
                return;
            }

            lock (this._lock)
            {
                var before = this._latestKnown ?? this._latestAtLastRun;
                if (this._latestKnown >= latest)
                {
                    return;
                }

                this._latestKnown = latest;

                // A first run: nothing is new to someone who has never seen a comic here.
                this._newestShown ??= latest;

                if (this.HasUnreadLocked() && (!before.HasValue || latest > before.Value))
                {
                    this._flashStartedAt = this._timeProvider.GetUtcNow();
                }
            }

            this.RaiseChanged();
        }

        // Call when a comic appears on the keypad, as its picture or its title card.
        public void MarkShown(Int32 num)
        {
            lock (this._lock)
            {
                if (num < ComicNumbers.First || this._newestShown >= num)
                {
                    return;
                }

                this._newestShown = num;
                if (!this.HasUnreadLocked())
                {
                    this._flashStartedAt = null;
                }
            }

            this.RaiseChanged();
        }

        private Boolean HasUnreadLocked()
        {
            var latest = this._latestKnown ?? this._latestAtLastRun;
            return latest.HasValue && this._newestShown.HasValue && latest.Value > this._newestShown.Value;
        }

        private Boolean IsFlashingLocked(DateTimeOffset now) =>
            this._flashStartedAt.HasValue && this.HasUnreadLocked() && now - this._flashStartedAt.Value < this._flashDuration;

        // One handler failing doesn't stop the others.
        private void RaiseChanged() => EventRaiser.Raise(this.Changed, this, this._log, "NewComicTracker.Changed");
    }
}
