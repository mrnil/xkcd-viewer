namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Threading.Tasks;

    // The New Comic key (see docs/ai/product.md, "The new-comic signal"). With nothing new it shows the logo with its
    // eyes closed. When a new comic is found it flashes between that and the open-eyed logo on green with a "new"
    // banner, and stays green until the new comic has appeared on the keypad. Pressing it opens a comic in the
    // browser: the first unread one while something is unread (which doesn't count as seen, so the key stays green),
    // otherwise the latest.
    //
    // A command rather than a folder because a folder key can't be redrawn once it's shown (hardware spike,
    // 2026-10-11), and this key has to change. Users place this class by name, so don't rename it or its namespace.
    public class NewComicCommand : PluginDynamicCommand
    {
        private xkcdviewerPlugin _subscribedTo;

        public NewComicCommand()
            : base("New Comic", "Shows when a new xkcd comic is out; press to read it", "xkcd viewer", DeviceType.All)
        {
            // Image only: without this the SDK draws the display name over the image (sdk-quick-reference.md).
            this.IsWidget = true;
        }

        protected override Boolean OnLoad()
        {
            if (xkcdviewerPlugin.Instance is { } plugin)
            {
                plugin.NewComicLookChanged += this.OnNewComicLookChanged;
                this._subscribedTo = plugin;
            }

            return base.OnLoad();
        }

        protected override Boolean OnUnload()
        {
            this._subscribedTo?.NewComicLookChanged -= this.OnNewComicLookChanged;
            this._subscribedTo = null;
            return base.OnUnload();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var green = xkcdviewerPlugin.Instance?.NewComics?.Look == NewComicLook.Green;
            return green ? KeyRenderer.Logo(imageSize, "new", green: true) : KeyRenderer.Logo(imageSize, eyesClosed: true);
        }

        protected override void RunCommand(String actionParameter)
        {
            var plugin = xkcdviewerPlugin.Instance;
            if (plugin?.NewComics == null || plugin.Comics == null)
            {
                return;
            }

            if ((plugin.NewComics.FirstUnread ?? plugin.NewComics.LatestKnown) is Int32 num)
            {
                BrowserLauncher.OpenComicPage(xkcdUrls.Page(num), plugin.ServiceLog);
                return;
            }

            // Nothing known yet (a first run, offline at load): ask, then open whatever comes back.
            _ = Task.Run(async () =>
            {
                try
                {
                    if (await plugin.Comics.RefreshLatestAsync() is { } latest)
                    {
                        BrowserLauncher.OpenComicPage(latest.PageUrl, plugin.ServiceLog);
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to open the latest comic");
                }
            });
        }

        private void OnNewComicLookChanged(Object sender, EventArgs e) => this.ActionImageChanged();
    }
}
