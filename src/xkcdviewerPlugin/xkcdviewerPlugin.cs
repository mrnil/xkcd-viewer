namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Threading;

    // Owns the services and remembers, across sessions, the last comic viewed and what the new-comic signal needs
    // (the newest comic shown and the latest known). Kept thin: everything it does is wiring that needs the plugin
    // runtime.
    public class xkcdviewerPlugin : Plugin
    {
        private const String LastViewedComicSetting = "LastViewedComic";
        private const String NewestShownComicSetting = "NewestShownComic";
        private const String LatestKnownComicSetting = "LatestKnownComic";

        private readonly Object _settingsLock = new Object();
        private Int32? _savedNum;
        private Int32? _savedNewestShown;
        private Int32? _savedLatestKnown;

        private NewComicPoller _poller;
        private Timer _flashTimer;

        public xkcdviewerPlugin()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
            xkcdviewerPlugin.Instance = this;
        }

        // Raised when the New Comic key and the Comic Browser's Next chevron should be redrawn: something became
        // unread or read, or the flash moved on. Actions subscribe; it exists before Load, unlike the services.
        public event EventHandler NewComicLookChanged;

        // Actions can be constructed before Load runs, so they must null-check this, Comics and NewComics.
        public static xkcdviewerPlugin Instance { get; private set; }

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        // For services, which take IPluginLog so tests can check what they log.
        public IPluginLog ServiceLog { get; } = new PluginLogAdapter();

        public ComicService Comics { get; private set; }

        public NewComicTracker NewComics { get; private set; }

        public override void Load()
        {
            this._savedNum = this.ReadComicSetting(LastViewedComicSetting);
            this._savedNewestShown = this.ReadComicSetting(NewestShownComicSetting);
            this._savedLatestKnown = this.ReadComicSetting(LatestKnownComicSetting);

            this.Comics = new ComicService(new xkcdClient(this.ServiceLog), this.ServiceLog, this._savedNum);
            this.Comics.Changed += this.OnComicChanged;

            this.NewComics = new NewComicTracker(this.ServiceLog, this._savedNewestShown, this._savedLatestKnown);
            this.NewComics.Changed += this.OnNewComicsChanged;
            this.Comics.LatestKnown += this.OnLatestKnown;

            // Each installation checks at its own minute past the hour, so they don't all ask xkcd at once.
            var offset = TimeSpan.FromSeconds(Random.Shared.Next((Int32)TimeSpan.FromHours(1).TotalSeconds));
            this._poller = new NewComicPoller(this.Comics.RefreshLatestAsync, this.ServiceLog, offset);
            this._poller.Start();

            PluginLog.Info($"Loaded; starting at comic {(this._savedNum.HasValue ? ComicNumbers.FormatSaved(this._savedNum.Value) : "latest")}; "
                + $"checking for new comics at {offset:mm\\:ss} past the hour");
        }

        public override void Unload()
        {
            this._poller?.Dispose();
            this._poller = null;

            // Cleared as well as disposed: OnNewComicsChanged only starts a timer when there's none, so a disposed one
            // left here would stop the flash from animating if this instance were loaded again.
            lock (this._settingsLock)
            {
                this._flashTimer?.Dispose();
                this._flashTimer = null;
            }

            this.Comics?.Changed -= this.OnComicChanged;
            this.Comics?.LatestKnown -= this.OnLatestKnown;
            this.NewComics?.Changed -= this.OnNewComicsChanged;
        }

        private Int32? ReadComicSetting(String name) =>
            this.TryGetPluginSetting(name, out var saved) ? ComicNumbers.ParseSaved(saved) : null;

        // Saved locally only: which comic was open isn't worth backing up to the cloud.
        private void OnComicChanged(Object sender, EventArgs e)
        {
            if (this.Comics.View.Num is not Int32 num)
            {
                return;
            }

            lock (this._settingsLock)
            {
                if (num == this._savedNum)
                {
                    return;
                }

                this._savedNum = num;
                this.SetPluginSetting(LastViewedComicSetting, ComicNumbers.FormatSaved(num), false);
            }
        }

        private void OnLatestKnown(Object sender, Int32 latest) => this.NewComics?.OnLatestKnown(latest);

        private void OnNewComicsChanged(Object sender, EventArgs e)
        {
            var tracker = this.NewComics;
            if (tracker == null)
            {
                return;
            }

            lock (this._settingsLock)
            {
                if (tracker.NewestShown is Int32 shown && shown != this._savedNewestShown)
                {
                    this._savedNewestShown = shown;
                    this.SetPluginSetting(NewestShownComicSetting, ComicNumbers.FormatSaved(shown), false);
                }

                if (tracker.LatestKnown is Int32 latest && latest != this._savedLatestKnown)
                {
                    this._savedLatestKnown = latest;
                    this.SetPluginSetting(LatestKnownComicSetting, ComicNumbers.FormatSaved(latest), false);
                }

                // Keep redrawing while the keys flash; the timer stops itself once the flash is over.
                if (tracker.IsFlashing && this._flashTimer == null)
                {
                    this._flashTimer = new Timer(_ => this.OnFlashTick(), null, PluginTimings.NewComicFlashInterval, PluginTimings.NewComicFlashInterval);
                }
            }

            this.RaiseNewComicLookChanged();
        }

        private void OnFlashTick()
        {
            if (this.NewComics?.IsFlashing != true)
            {
                lock (this._settingsLock)
                {
                    this._flashTimer?.Dispose();
                    this._flashTimer = null;
                }
            }

            // Once more after the flash ends, so the keys settle on steady green.
            this.RaiseNewComicLookChanged();
        }

        // Each handler on its own, so the New Comic key failing to redraw can't stop the Next chevron, or the reverse.
        private void RaiseNewComicLookChanged() =>
            EventRaiser.Raise(this.NewComicLookChanged, this, this.ServiceLog, nameof(this.NewComicLookChanged));
    }
}
