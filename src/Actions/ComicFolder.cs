namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    // The xkcd folder: Prev / Next on the top row and the current comic split across the 3x2 block below. Laid out
    // per device (DeviceGrid, FolderLayout); what each key shows comes from KeyContent. See docs/ai/product.md.
    //
    // Users place this class by name, so renaming it or its namespace orphans their buttons.
    public class ComicFolder : PluginDynamicFolder
    {
        private readonly Object _areaLock = new Object();

        // The full comic area per comic and key size, kept private so disposing an evicted one is always safe (the
        // SDK only ever gets fresh tiles cut from it). A null value means "title card".
        private readonly LruCache<(Int32 Num, Int32 TileWidth, Int32 TileHeight), BitmapImage> _areas =
            new LruCache<(Int32 Num, Int32 TileWidth, Int32 TileHeight), BitmapImage>(ComicService.MaxCachedImages, area => area?.Dispose());

        private readonly ConcurrentDictionary<String, Byte> _actionNames = new ConcurrentDictionary<String, Byte>();
        private ComicService _subscribedTo;

        public ComicFolder()
        {
            this.DisplayName = "xkcd";
            this.GroupName = "xkcd";
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.ButtonArea;

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            if (!DeviceGrid.IsKnown(deviceType))
            {
                PluginLog.Info($"No grid recorded for device {deviceType} (0x{(UInt32)deviceType:X}) - assuming {DeviceGrid.FallbackColumns} columns");
            }

            var names = FolderLayout.BuildSlots(DeviceGrid.GetColumns(deviceType)).Select(slot => slot.ActionName).ToList();
            foreach (var name in names)
            {
                this._actionNames[name] = 0;
            }

            return names.Select(this.CreateCommandName);
        }

        // An empty name stops the SDK shrinking the image to fit a caption underneath (hardware spike).
        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => String.Empty;

        public override BitmapImage GetButtonImage(PluginImageSize imageSize) => KeyRenderer.Logo(imageSize);

        public override Boolean Activate()
        {
            var service = xkcdviewerPlugin.Instance?.Comics;
            if (service != null)
            {
                if (this._subscribedTo == null)
                {
                    service.Changed += this.OnComicChanged;
                    this._subscribedTo = service;
                }

                _ = service.StartAsync();
            }

            return base.Activate();
        }

        public override Boolean Deactivate()
        {
            this.Unsubscribe();
            return base.Deactivate();
        }

        public override Boolean Unload()
        {
            this.Unsubscribe();
            lock (this._areaLock)
            {
                this._areas.Clear();
            }

            return base.Unload();
        }

        public override void RunCommand(String actionParameter)
        {
            var service = xkcdviewerPlugin.Instance?.Comics;
            if (service == null || !FolderSlot.TryParse(actionParameter, out var slot))
            {
                return;
            }

            switch (slot.Kind)
            {
                case FolderSlotKind.Previous:
                    _ = service.PreviousAsync();
                    break;
                case FolderSlotKind.Next:
                    _ = service.NextAsync();
                    break;
                case FolderSlotKind.ComicTile when service.View.Num is Int32 num:
                    BrowserLauncher.OpenComicPage(xkcdUrls.Page(num), xkcdviewerPlugin.Instance.ServiceLog);
                    break;
            }
        }

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var service = xkcdviewerPlugin.Instance?.Comics;
            if (service == null || !FolderSlot.TryParse(actionParameter, out var slot))
            {
                return null;
            }

            try
            {
                var view = service.View;
                var content = KeyContent.For(slot, view);
                return content.Kind switch
                {
                    KeyContentKind.PreviousArrow => KeyRenderer.Arrow(false, content.Enabled, imageSize),
                    KeyContentKind.NextArrow => KeyRenderer.Arrow(true, content.Enabled, imageSize),
                    KeyContentKind.ComicTile => this.DrawComicTile(view, content.TileIndex, imageSize),
                    KeyContentKind.TitleCardTile => KeyRenderer.TitleCardTile(content.Text, content.TileIndex, imageSize),
                    KeyContentKind.LoadingTile => KeyRenderer.StatusTile(content.Text, true, imageSize),
                    KeyContentKind.OfflineTile => KeyRenderer.StatusTile(content.Text, false, imageSize),

                    // Padding: null leaves the device's default empty key.
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to draw key '{actionParameter}' at {imageSize}");
                return null;
            }
        }

        private BitmapImage DrawComicTile(ComicView view, Int32 tileIndex, PluginImageSize imageSize)
        {
            var (tileWidth, tileHeight) = KeyRenderer.TileSize(imageSize);
            var key = (view.Comic.Num, tileWidth, tileHeight);

            lock (this._areaLock)
            {
                if (!this._areas.TryGet(key, out var area))
                {
                    area = KeyRenderer.BuildComicArea(view.Comic.Num, view.Image, tileWidth, tileHeight, xkcdviewerPlugin.Instance.ServiceLog);
                    this._areas.Add(key, area);
                }

                if (area != null)
                {
                    return KeyRenderer.ComicTile(area, tileIndex, imageSize);
                }
            }

            var text = TitleCardLayout.Layout(view.Comic.Num, view.Comic.Title)[tileIndex];
            return KeyRenderer.TitleCardTile(text, tileIndex, imageSize);
        }

        // All keys at once: the hardware spike showed they update together, with no visible tearing.
        private void OnComicChanged(Object sender, EventArgs e)
        {
            foreach (var name in this._actionNames.Keys)
            {
                this.CommandImageChanged(name);
            }
        }

        private void Unsubscribe()
        {
            this._subscribedTo?.Changed -= this.OnComicChanged;
            this._subscribedTo = null;
        }
    }
}
