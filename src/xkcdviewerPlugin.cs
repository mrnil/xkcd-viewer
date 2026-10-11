namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    // Owns the services and remembers the last comic viewed across sessions. Kept thin: everything it does is
    // wiring that needs the plugin runtime.
    public class xkcdviewerPlugin : Plugin
    {
        private const String LastViewedComicSetting = "LastViewedComic";

        private Int32? _savedNum;

        public xkcdviewerPlugin()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
            xkcdviewerPlugin.Instance = this;
        }

        // Actions can be constructed before Load runs, so they must null-check this and Comics.
        public static xkcdviewerPlugin Instance { get; private set; }

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        // For services, which take IPluginLog so tests can check what they log.
        public IPluginLog ServiceLog { get; } = new PluginLogAdapter();

        public ComicService Comics { get; private set; }

        public override void Load()
        {
            this._savedNum = this.TryGetPluginSetting(LastViewedComicSetting, out var saved) ? ComicNumbers.ParseSaved(saved) : null;
            this.Comics = new ComicService(new xkcdClient(this.ServiceLog), this.ServiceLog, this._savedNum);
            this.Comics.Changed += this.OnComicChanged;
            PluginLog.Info($"Loaded; starting at comic {(this._savedNum.HasValue ? ComicNumbers.FormatSaved(this._savedNum.Value) : "latest")}");
        }

        public override void Unload()
        {
            this.Comics?.Changed -= this.OnComicChanged;
        }

        // Saved locally only: which comic was open isn't worth backing up to the cloud.
        private void OnComicChanged(Object sender, EventArgs e)
        {
            if (this.Comics.View.Num is not Int32 num || num == this._savedNum)
            {
                return;
            }

            this._savedNum = num;
            this.SetPluginSetting(LastViewedComicSetting, ComicNumbers.FormatSaved(num), false);
        }
    }
}
