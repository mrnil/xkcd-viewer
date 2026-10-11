namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    // Which comic is showing, plus everything fetched so far. Comic details are kept for the whole session (a
    // published comic never changes); image bytes are kept in a size-limited ImageCache. Whenever the current comic
    // changes, the comics either side of it are fetched in the background, so Prev / Next are usually instant.
    //
    // The latest comic is fetched once per session: it's where Next stops, and it's the starting comic when there's
    // no last-viewed one. It isn't checked again for newer comics.
    //
    // Thread-safe. SDK callbacks can call in from any thread; Changed is raised on a background thread.
    public sealed class ComicService
    {
        public const Int32 MaxCachedImages = 20;
        public const Int64 MaxCachedImageBytes = 16 * 1024 * 1024;

        private readonly IxkcdClient _client;
        private readonly IPluginLog _log;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _retryDelay;

        private readonly Object _lock = new Object();
        private readonly Dictionary<Int32, xkcdComic> _comics = new Dictionary<Int32, xkcdComic>();
        private readonly ImageCache _images = new ImageCache(MaxCachedImages, MaxCachedImageBytes);
        private readonly Dictionary<Int32, Task> _loads = new Dictionary<Int32, Task>();
        private readonly Dictionary<Int32, DateTimeOffset> _failedUntil = new Dictionary<Int32, DateTimeOffset>();

        private Int32? _currentNum;
        private Int32? _latestNum;
        private Task _latestLoad;
        private DateTimeOffset? _latestFailedUntil;

        // startNum is the last comic viewed, from a previous session; null (or not a comic number) starts at the latest.
        public ComicService(IxkcdClient client, IPluginLog log, Int32? startNum)
            : this(client, log, startNum, TimeProvider.System, PluginTimings.FailedLoadRetryDelay)
        {
        }

        internal ComicService(IxkcdClient client, IPluginLog log, Int32? startNum, TimeProvider timeProvider, TimeSpan retryDelay)
        {
            this._client = client ?? throw new ArgumentNullException(nameof(client));
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this._retryDelay = retryDelay;
            this._currentNum = startNum >= ComicNumbers.First ? startNum : null;
        }

        // Raised when what View returns may have changed: another comic is current, the current one finished
        // loading or failed, or Prev / Next became possible.
        public event EventHandler Changed;

        public ComicView View
        {
            get
            {
                lock (this._lock)
                {
                    return this.BuildView();
                }
            }
        }

        // Call when the folder opens. Fetches the latest comic if it isn't known yet, then the current comic and
        // its neighbours. The returned task completes when all of that has finished; it never faults.
        public Task StartAsync() => Task.WhenAll(this.EnsureLatestAsync(), this.LoadCurrentAndNeighboursAsync());

        public Task PreviousAsync() => this.MoveAsync(current => ComicNumbers.Previous(current));

        // Before the latest comic is known there's no telling whether a next one exists, so Next asks for the
        // latest comic again (once the failure wait has passed) instead of moving.
        public Task NextAsync()
        {
            lock (this._lock)
            {
                if (!this._latestNum.HasValue)
                {
                    return this.EnsureLatestAsyncLocked();
                }
            }

            return this.MoveAsync(current => ComicNumbers.Next(current, this._latestNum.Value));
        }

        private Task MoveAsync(Func<Int32, Int32> step)
        {
            lock (this._lock)
            {
                if (!this._currentNum.HasValue)
                {
                    return Task.CompletedTask;
                }

                var target = step(this._currentNum.Value);
                if (target == this._currentNum.Value)
                {
                    return Task.CompletedTask;
                }

                this._currentNum = target;
            }

            this.RaiseChanged();
            return this.LoadCurrentAndNeighboursAsync();
        }

        private Task LoadCurrentAndNeighboursAsync()
        {
            var loads = new List<Task>(3);
            lock (this._lock)
            {
                if (!this._currentNum.HasValue)
                {
                    return Task.CompletedTask;
                }

                var current = this._currentNum.Value;
                loads.Add(this.EnsureLoadedLocked(current));

                var previous = ComicNumbers.Previous(current);
                if (previous != current)
                {
                    loads.Add(this.EnsureLoadedLocked(previous));
                }

                if (this._latestNum.HasValue)
                {
                    var next = ComicNumbers.Next(current, this._latestNum.Value);
                    if (next != current)
                    {
                        loads.Add(this.EnsureLoadedLocked(next));
                    }
                }
            }

            return Task.WhenAll(loads);
        }

        // One load per comic at a time: a second caller gets the load already running. Load is started with Task.Run
        // so it can't finish (and remove itself from _loads) before it has been added.
        private Task EnsureLoadedLocked(Int32 num)
        {
            if (this.IsCompleteLocked(num))
            {
                return Task.CompletedTask;
            }

            if (this._loads.TryGetValue(num, out var running))
            {
                return running;
            }

            if (this._failedUntil.TryGetValue(num, out var until) && this._timeProvider.GetUtcNow() < until)
            {
                return Task.CompletedTask;
            }

            var load = Task.Run(() => this.LoadAsync(num));
            this._loads[num] = load;
            return load;
        }

        private Boolean IsCompleteLocked(Int32 num) =>
            this._comics.TryGetValue(num, out var comic) && (comic.ImageUrl == null || this._images.Contains(num));

        private async Task LoadAsync(Int32 num)
        {
            var succeeded = false;
            try
            {
                xkcdComic comic;
                lock (this._lock)
                {
                    this._comics.TryGetValue(num, out comic);
                }

                if (comic == null)
                {
                    comic = await this._client.GetComicAsync(num);
                    if (comic == null)
                    {
                        return;
                    }

                    lock (this._lock)
                    {
                        this._comics[num] = comic;
                    }
                }

                if (comic.ImageUrl != null)
                {
                    var image = await this._client.GetImageAsync(comic.ImageUrl);
                    if (image == null)
                    {
                        return;
                    }

                    lock (this._lock)
                    {
                        this._images.Add(num, image);
                    }
                }

                succeeded = true;
            }
            catch (Exception ex)
            {
                this._log.Error($"Failed to load comic {num}: {ex.Message}");
            }
            finally
            {
                lock (this._lock)
                {
                    this._loads.Remove(num);
                    if (succeeded)
                    {
                        this._failedUntil.Remove(num);
                    }
                    else
                    {
                        this._failedUntil[num] = this._timeProvider.GetUtcNow() + this._retryDelay;
                    }
                }

                // In the finally, so the early returns on failure still tell the folder to redraw.
                this.RaiseChangedIfCurrent(num);
            }
        }

        private Task EnsureLatestAsync()
        {
            lock (this._lock)
            {
                return this.EnsureLatestAsyncLocked();
            }
        }

        private Task EnsureLatestAsyncLocked()
        {
            if (this._latestNum.HasValue)
            {
                return Task.CompletedTask;
            }

            if (this._latestLoad != null)
            {
                return this._latestLoad;
            }

            if (this._latestFailedUntil.HasValue && this._timeProvider.GetUtcNow() < this._latestFailedUntil.Value)
            {
                return Task.CompletedTask;
            }

            this._latestLoad = Task.Run(this.LoadLatestAsync);
            return this._latestLoad;
        }

        private async Task LoadLatestAsync()
        {
            xkcdComic latest = null;
            try
            {
                latest = await this._client.GetLatestAsync();
            }
            catch (Exception ex)
            {
                this._log.Error($"Failed to load the latest comic: {ex.Message}");
            }

            lock (this._lock)
            {
                this._latestLoad = null;
                if (latest == null)
                {
                    this._latestFailedUntil = this._timeProvider.GetUtcNow() + this._retryDelay;
                }
                else
                {
                    this._latestFailedUntil = null;
                    this._latestNum = latest.Num;
                    this._comics[latest.Num] = latest;

                    // A last-viewed number beyond the latest can only come from a corrupt setting; there's nothing there.
                    if (!this._currentNum.HasValue || this._currentNum.Value > latest.Num)
                    {
                        this._currentNum = latest.Num;
                    }
                }
            }

            this.RaiseChanged();

            if (latest != null)
            {
                await this.LoadCurrentAndNeighboursAsync();
            }
        }

        private ComicView BuildView()
        {
            if (!this._currentNum.HasValue)
            {
                var state = this._latestLoad == null && this._latestFailedUntil.HasValue ? ComicViewState.Failed : ComicViewState.Loading;
                return new ComicView(null, state, null, null, false, false);
            }

            var num = this._currentNum.Value;
            var loading = this._loads.ContainsKey(num);
            var canGoPrevious = ComicNumbers.Previous(num) != num;
            var canGoNext = this._latestNum.HasValue && ComicNumbers.Next(num, this._latestNum.Value) != num;

            if (!this._comics.TryGetValue(num, out var comic))
            {
                var state = !loading && this._failedUntil.ContainsKey(num) ? ComicViewState.Failed : ComicViewState.Loading;
                return new ComicView(num, state, null, null, canGoPrevious, canGoNext);
            }

            // An image that failed doesn't hold the comic back: it shows as Ready without one, and the title card.
            Byte[] image = null;
            var imageStillComing = comic.ImageUrl != null && !this._images.TryGet(num, out image) && !this._failedUntil.ContainsKey(num);
            return new ComicView(num, imageStillComing ? ComicViewState.Loading : ComicViewState.Ready, comic, image, canGoPrevious, canGoNext);
        }

        private void RaiseChangedIfCurrent(Int32 num)
        {
            Boolean isCurrent;
            lock (this._lock)
            {
                isCurrent = this._currentNum == num;
            }

            if (isCurrent)
            {
                this.RaiseChanged();
            }
        }

        // Never under the lock: a handler will call View. One handler failing doesn't stop the others.
        private void RaiseChanged()
        {
            var handlers = this.Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    this._log.Error($"A Changed handler failed: {ex.Message}");
                }
            }
        }
    }
}
