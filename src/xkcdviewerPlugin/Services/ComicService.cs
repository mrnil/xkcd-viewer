namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    // Which comic is showing, plus everything fetched so far. Comic details are kept for the whole session (a
    // published comic never changes); image bytes are kept in a size-limited ImageCache. Whenever the current comic
    // changes, the PreloadDistance comics either side of it are fetched in the background, so Prev / Next are usually
    // instant, even when pressed twice quickly.
    //
    // The latest comic is where Next stops, the starting comic when there's no last-viewed one, and the bound for
    // random picks. Browsing fetches it once per session; ShowLatestAsync asks xkcd again every time, so it always
    // lands on the newest comic published.
    //
    // ShowLatestAsync and ShowRandomAsync jump to a comic chosen from the latest one. Until that answer arrives they
    // show no comic (loading, or offline if it fails) rather than an unrelated one; if asking again fails, the latest
    // already known will do. Showing a random comic preloads the next random pick instead of the neighbours, so
    // "show another" is usually instant too. Every jump becomes the current comic, which is also the last viewed.
    //
    // Thread-safe. SDK callbacks can call in from any thread; Changed is raised on a background thread.
    public sealed class ComicService
    {
        public const Int32 PreloadDistance = 2;

        // The preloaded window (current plus PreloadDistance either side) plus one spare each way, so the comic
        // just stepped away from is still cached when the user turns straight back.
        public const Int32 MaxCachedImages = 2 * PreloadDistance + 1 + 2;

        // A safety net for unusually large images; seven typical comics come to about 1 MB.
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

        private readonly Func<Int32, Int32, Int32> _randomBetween;

        private Int32? _currentNum;
        private Int32? _latestNum;

        // What to show once the latest comic is known, when ShowLatestAsync or ShowRandomAsync came first.
        private Jump _waitingToJump;

        // Whether the current comic was picked at random: it decides what gets preloaded.
        private Boolean _showingRandom;
        private Int32? _nextRandomNum;
        private Task _latestLoad;
        private DateTimeOffset? _latestFailedUntil;

        // startNum is the last comic viewed, from a previous session; null (or not a comic number) starts at the latest.
        public ComicService(IxkcdClient client, IPluginLog log, Int32? startNum)
            : this(client, log, startNum, TimeProvider.System, PluginTimings.FailedLoadRetryDelay)
        {
        }

        // randomBetween follows Random.Next(min, max): min inclusive, max exclusive. Null uses Random.Shared.
        internal ComicService(
            IxkcdClient client, IPluginLog log, Int32? startNum, TimeProvider timeProvider, TimeSpan retryDelay, Func<Int32, Int32, Int32> randomBetween = null)
        {
            this._client = client ?? throw new ArgumentNullException(nameof(client));
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this._retryDelay = retryDelay;
            this._randomBetween = randomBetween ?? Random.Shared.Next;
            this._currentNum = startNum >= ComicNumbers.First ? startNum : null;
        }

        private enum Jump
        {
            None,
            First,
            Latest,
            Random,
        }

        // Raised when what View returns may have changed: another comic is current, the current one finished
        // loading or failed, or Prev / Next became possible.
        public event EventHandler Changed;

        // Raised with the latest comic's number each time xkcd answers a request for it, by any route (opening a
        // folder, a jump, or RefreshLatestAsync). The NewComicTracker listens. Raised on a background thread.
        public event EventHandler<Int32> LatestKnown;

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

        // Call when the browsing folder opens: it shows the current comic (the last one viewed). Fetches the latest
        // comic if it isn't known yet, then the current comic and its neighbours. The returned task completes when all
        // of that has finished; it never faults.
        public Task StartAsync()
        {
            Boolean wasWaiting;
            lock (this._lock)
            {
                wasWaiting = this._waitingToJump != Jump.None;
                this._waitingToJump = Jump.None;
                this._showingRandom = false;
            }

            if (wasWaiting)
            {
                this.RaiseChanged();
            }

            return Task.WhenAll(this.EnsureLatestAsync(), this.LoadCurrentAndNeighboursAsync());
        }

        // Shows the newest comic published (asking xkcd again first), with Prev / Next from there. Its neighbours are
        // preloaded as usual.
        public Task ShowLatestAsync() => this.JumpAsync(Jump.Latest);

        // Shows comic 1, with Prev / Next from there.
        public Task ShowFirstAsync() => this.JumpAsync(Jump.First);

        // Shows a random comic other than the current one, and preloads the next random pick.
        public Task ShowRandomAsync() => this.JumpAsync(Jump.Random);

        public Task PreviousAsync() => this.MoveAsync(current => ComicNumbers.Previous(current));

        // Shows a given comic (the first unread one, for the green Next chevron), kept within 1..latest. Does nothing
        // before the latest comic is known.
        public Task ShowComicAsync(Int32 num)
        {
            Int32 latest;
            lock (this._lock)
            {
                if (!this._latestNum.HasValue)
                {
                    return Task.CompletedTask;
                }

                latest = this._latestNum.Value;
            }

            return this.MoveAsync(_ => Math.Clamp(num, ComicNumbers.First, latest));
        }

        // Asks xkcd for the latest comic without changing what's showing, for the new-comic check. Returns the latest
        // comic, or null if this request failed (or one failed less than the retry delay ago, so none was sent).
        public async Task<xkcdComic> RefreshLatestAsync()
        {
            Task load;
            lock (this._lock)
            {
                load = this.EnsureLatestAsyncLocked(refresh: true);
            }

            await load.ConfigureAwait(false);

            lock (this._lock)
            {
                return !this._latestFailedUntil.HasValue && this._latestNum is Int32 latest && this._comics.TryGetValue(latest, out var comic)
                    ? comic
                    : null;
            }
        }

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

        private Task JumpAsync(Jump jump)
        {
            Task latestLoad = null;
            lock (this._lock)
            {
                this._showingRandom = jump == Jump.Random;

                // Comic 1 needs nothing first. A random pick needs some latest comic to bound it; the latest jump
                // needs a fresh one.
                var waitForLatest = false;
                if (jump == Jump.Latest || (jump == Jump.Random && !this._latestNum.HasValue))
                {
                    latestLoad = this.EnsureLatestAsyncLocked(refresh: true);
                    waitForLatest = this._latestLoad != null;
                }

                if (waitForLatest)
                {
                    this._waitingToJump = jump;
                }
                else if (jump == Jump.First || this._latestNum.HasValue)
                {
                    // Also the latest jump within the failure wait: the latest already known is the best answer.
                    this._waitingToJump = Jump.None;
                    this.JumpLocked(jump);
                    latestLoad = null;
                }
                else
                {
                    // Failed recently and nothing known: offline until asked again after the wait.
                    this._waitingToJump = jump;
                }
            }

            this.RaiseChanged();

            // LoadLatestAsync makes the jump, and loads what it lands on, once the latest comic arrives.
            return latestLoad ?? this.LoadCurrentAndNeighboursAsync();
        }

        private void JumpLocked(Jump jump)
        {
            if (jump == Jump.First)
            {
                this._currentNum = ComicNumbers.First;
                return;
            }

            var latest = this._latestNum.Value;
            if (jump == Jump.Latest)
            {
                this._currentNum = latest;
                return;
            }

            var target = this._nextRandomNum is Int32 picked && picked <= latest && picked != this._currentNum
                ? picked
                : ComicNumbers.PickRandom(this._randomBetween, latest, this._currentNum);
            this._currentNum = target;
            this._nextRandomNum = ComicNumbers.PickRandom(this._randomBetween, latest, target);
        }

        private Task MoveAsync(Func<Int32, Int32> step)
        {
            lock (this._lock)
            {
                if (!this._currentNum.HasValue || this._waitingToJump != Jump.None)
                {
                    return Task.CompletedTask;
                }

                var target = step(this._currentNum.Value);
                if (target == this._currentNum.Value)
                {
                    return Task.CompletedTask;
                }

                this._currentNum = target;
                this._showingRandom = false;
            }

            this.RaiseChanged();
            return this.LoadCurrentAndNeighboursAsync();
        }

        private Task LoadCurrentAndNeighboursAsync()
        {
            var loads = new List<Task>(2 * PreloadDistance + 1);
            lock (this._lock)
            {
                if (!this._currentNum.HasValue)
                {
                    return Task.CompletedTask;
                }

                var current = this._currentNum.Value;
                loads.Add(this.EnsureLoadedLocked(current));

                // A random comic's neighbours are no more likely to be shown next than any other comic.
                if (this._showingRandom)
                {
                    if (this._nextRandomNum is Int32 nextRandom && nextRandom != current)
                    {
                        loads.Add(this.EnsureLoadedLocked(nextRandom));
                    }

                    return Task.WhenAll(loads);
                }

                // Nearest first, so the comic one press away starts fetching before the one two presses away.
                var previous = current;
                var next = current;
                for (var distance = 1; distance <= PreloadDistance; distance++)
                {
                    var stepBack = ComicNumbers.Previous(previous);
                    if (stepBack != previous)
                    {
                        previous = stepBack;
                        loads.Add(this.EnsureLoadedLocked(previous));
                    }

                    var stepForward = this._latestNum.HasValue ? ComicNumbers.Next(next, this._latestNum.Value) : next;
                    if (stepForward != next)
                    {
                        next = stepForward;
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

        // refresh asks xkcd again even when a latest comic is already known.
        private Task EnsureLatestAsyncLocked(Boolean refresh = false)
        {
            if (this._latestNum.HasValue && !refresh)
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

            var landed = latest != null;
            lock (this._lock)
            {
                this._latestLoad = null;
                if (latest == null)
                {
                    this._latestFailedUntil = this._timeProvider.GetUtcNow() + this._retryDelay;

                    // Asking again failed, but an earlier answer is still a comic to show.
                    if (this._waitingToJump != Jump.None && this._latestNum.HasValue)
                    {
                        this.JumpLocked(this._waitingToJump);
                        this._waitingToJump = Jump.None;
                        landed = true;
                    }
                }
                else
                {
                    this._latestFailedUntil = null;

                    // xkcd's numbers only grow; never let a stale answer pull Next's limit back.
                    this._latestNum = Math.Max(this._latestNum ?? latest.Num, latest.Num);
                    this._comics[latest.Num] = latest;

                    if (this._waitingToJump != Jump.None)
                    {
                        this.JumpLocked(this._waitingToJump);
                        this._waitingToJump = Jump.None;
                    }

                    // A last-viewed number beyond the latest can only come from a corrupt setting; there's nothing there.
                    else if (!this._currentNum.HasValue || this._currentNum.Value > latest.Num)
                    {
                        this._currentNum = latest.Num;
                    }
                }
            }

            this.RaiseChanged();
            if (latest != null)
            {
                this.RaiseLatestKnown(latest.Num);
            }

            if (landed)
            {
                await this.LoadCurrentAndNeighboursAsync();
            }
        }

        private ComicView BuildView()
        {
            if (!this._currentNum.HasValue || this._waitingToJump != Jump.None)
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

        private void RaiseLatestKnown(Int32 latest)
        {
            var handlers = this.LatestKnown;
            if (handlers == null)
            {
                return;
            }

            foreach (EventHandler<Int32> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, latest);
                }
                catch (Exception ex)
                {
                    this._log.Error($"A LatestKnown handler failed: {ex.Message}");
                }
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
