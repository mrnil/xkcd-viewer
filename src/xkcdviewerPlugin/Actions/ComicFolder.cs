namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    // The Comic Browser folder: Prev / Next on the top row and the current comic split across the 3x2 block below.
    // Laid out per device (DeviceGrid, FolderLayout); what each key shows comes from KeyContent. See docs/ai/product.md.
    // LatestComicFolder and RandomComicFolder are this folder in another ComicFolderMode.
    //
    // Tapping Prev / Next steps one comic; holding one for PluginTimings.HoldToJump jumps to the first / newest comic
    // instead (KeyHold tells the two apart). That needs the key's press and release, so Prev / Next are handled in
    // ProcessButtonEvent2 (the MX Keypad's physical keys) and ProcessTouchEvent (the Live S / CT touch keys, which send
    // no button events); RunCommand still steps for a key that sends neither.
    //
    // Users place this class (and its subclasses) by name, so renaming it or its namespace orphans their buttons.
    // The other folders subclass it, rather than sharing an abstract base, because the SDK docs don't say whether the
    // plugin service skips abstract action classes when it looks for actions.
    public class ComicFolder : PluginDynamicFolder
    {
        private readonly Object _areaLock = new Object();

        // The full comic area per comic and key size, kept private so disposing an evicted one is always safe (the
        // SDK only ever gets fresh tiles cut from it). A null value means "title card".
        private readonly LruCache<(Int32 Num, Int32 TileWidth, Int32 TileHeight), BitmapImage> _areas =
            new LruCache<(Int32 Num, Int32 TileWidth, Int32 TileHeight), BitmapImage>(ComicService.MaxCachedImages, area => area?.Dispose());

        private readonly ConcurrentDictionary<String, Byte> _actionNames = new ConcurrentDictionary<String, Byte>();

        // Prev / Next keys that send press and release events, as buttons or touches. RunCommand leaves those to
        // ProcessButtonEvent2 / ProcessTouchEvent, so a press can't step twice if the SDK calls both.
        private readonly ConcurrentDictionary<String, Byte> _keysWithButtonEvents = new ConcurrentDictionary<String, Byte>();
        private readonly KeyHold _hold = new KeyHold();
        private readonly ComicFolderMode _mode;
        private readonly String _keyLabel;
        private ComicService _subscribedTo;

        public ComicFolder()
            : this("Comic Browser", ComicFolderMode.Browse, null)
        {
        }

        // keyLabel is drawn under the logo on the key that opens the folder; null for none.
        protected ComicFolder(String displayName, ComicFolderMode mode, String keyLabel)
        {
            this._mode = mode;
            this._keyLabel = keyLabel;
            this.DisplayName = displayName;
            // Keep this: without a GroupName, Options+ files the folder under a generic "Control Center" group
            // (seen on hardware). PluginApi has no way to list it ungrouped.
            this.GroupName = "xkcd viewer";
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

        // The same for the key that opens the folder: without this, the SDK draws DisplayName ("Comic Browser") under
        // the logo and squeezes it. DisplayName itself stays, as the action's name in Options+.
        public override String GetButtonDisplayName(PluginImageSize imageSize) => String.Empty;

        public override BitmapImage GetButtonImage(PluginImageSize imageSize) => KeyRenderer.Logo(imageSize, this._keyLabel);

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

                _ = this._mode switch
                {
                    ComicFolderMode.Latest => service.ShowLatestAsync(),
                    ComicFolderMode.Random => service.ShowRandomAsync(),
                    _ => service.StartAsync(),
                };
            }

            return base.Activate();
        }

        public override Boolean Deactivate()
        {
            this.Unsubscribe();
            this._hold.Clear();
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
                case FolderSlotKind.Next when this._mode == ComicFolderMode.Random:
                    _ = service.ShowRandomAsync();
                    break;
                case FolderSlotKind.Previous or FolderSlotKind.Next
                    when this._mode != ComicFolderMode.Random && !this._keysWithButtonEvents.ContainsKey(actionParameter):
                    Step(service, slot.Kind);
                    break;
                case FolderSlotKind.ComicTile when service.View.Num is Int32 num:
                    BrowserLauncher.OpenComicPage(xkcdUrls.Page(num), xkcdviewerPlugin.Instance.ServiceLog);
                    break;
            }
        }

        // Prev / Next only, outside the Random folder; every other key returns false and gets the SDK's usual
        // handling (RunCommand). Returning true marks the event as handled.
        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            if (this._mode == ComicFolderMode.Random
                || buttonEvent == null
                || !FolderSlot.TryParse(actionParameter, out var slot)
                || slot.Kind is not (FolderSlotKind.Previous or FolderSlotKind.Next))
            {
                return false;
            }

            switch (buttonEvent.EventType)
            {
                case DeviceButtonEventType.Press:
                    this.OnPress(actionParameter, slot.Kind);
                    break;
                case DeviceButtonEventType.Release:
                    this.OnRelease(actionParameter, slot.Kind);
                    break;
            }

            return true;
        }

        // The Live S and CT keys are touch screens: they send no button events, so a hold never reached
        // ProcessButtonEvent2 (seen on hardware 2026-10-11). Their touches say when a finger goes down (Press) and lifts
        // (TouchUp, plus LongRelease after a long touch). The device also decides on its own that a touch is a hold: it
        // sends LongPress about 0.5 s in, with haptic feedback, so the jump happens then, matching what the user feels,
        // rather than after the keypad's 2 s. Every touch on Prev / Next is handled here, Tap and DoubleTap included, so
        // quick taps each step once rather than becoming a jump.
        public override Boolean ProcessTouchEvent(String actionParameter, DeviceTouchEvent touchEvent)
        {
            if (this._mode == ComicFolderMode.Random
                || touchEvent == null
                || !FolderSlot.TryParse(actionParameter, out var slot)
                || slot.Kind is not (FolderSlotKind.Previous or FolderSlotKind.Next))
            {
                return false;
            }

            switch (touchEvent.EventType)
            {
                case DeviceTouchEventType.Press:
                    this.OnPress(actionParameter, slot.Kind);
                    break;
                case DeviceTouchEventType.LongPress when this._hold.TryHoldNow(actionParameter):
                    var service = xkcdviewerPlugin.Instance?.Comics;
                    if (service != null)
                    {
                        _ = Jump(service, slot.Kind);
                    }

                    break;
                case DeviceTouchEventType.TouchUp or DeviceTouchEventType.LongRelease:
                    this.OnRelease(actionParameter, slot.Kind);
                    break;
            }

            return true;
        }

        private void OnPress(String actionParameter, FolderSlotKind kind)
        {
            this._keysWithButtonEvents[actionParameter] = 0;
            _ = this.JumpIfHeldAsync(actionParameter, this._hold.Press(actionParameter), kind);
        }

        // A release that ends a tap steps; one after a hold (the jump has happened) or with no press does nothing, so a
        // touch that sends both TouchUp and LongRelease still steps at most once.
        private void OnRelease(String actionParameter, FolderSlotKind kind)
        {
            var service = xkcdviewerPlugin.Instance?.Comics;
            if (this._hold.Release(actionParameter) && service != null)
            {
                Step(service, kind);
            }
        }

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            // Never return null: a folder key without an image shows the device's "action not available" warning
            // triangle (seen on the Live S / CT padding keys). Every fallback draws a blank key instead.
            var service = xkcdviewerPlugin.Instance?.Comics;
            if (service == null || !FolderSlot.TryParse(actionParameter, out var slot))
            {
                return KeyRenderer.Blank(imageSize);
            }

            try
            {
                var view = service.View;
                var content = KeyContent.For(slot, view, this._mode);
                return content.Kind switch
                {
                    KeyContentKind.PreviousArrow => KeyRenderer.Arrow(false, content.Enabled, imageSize),
                    KeyContentKind.NextArrow => KeyRenderer.Arrow(true, content.Enabled, imageSize),
                    KeyContentKind.ShowAnother => KeyRenderer.ShowAnother(imageSize),
                    KeyContentKind.ComicTile => this.DrawComicTile(view, content.TileIndex, imageSize),
                    KeyContentKind.TitleCardTile => KeyRenderer.TitleCardTile(content.Text, content.TileIndex, imageSize),
                    KeyContentKind.LoadingTile => KeyRenderer.StatusTile(content.Text, true, imageSize),
                    KeyContentKind.OfflineTile => KeyRenderer.StatusTile(content.Text, false, imageSize),
                    _ => KeyRenderer.Blank(imageSize),
                };
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to draw key '{actionParameter}' at {imageSize}");
                return KeyRenderer.Blank(imageSize);
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

        private static void Step(ComicService service, FolderSlotKind kind) =>
            _ = kind == FolderSlotKind.Next ? service.NextAsync() : service.PreviousAsync();

        // Next jumps to the newest comic published, Prev to comic #1.
        private static Task Jump(ComicService service, FolderSlotKind kind) =>
            kind == FolderSlotKind.Next ? service.ShowLatestAsync() : service.ShowFirstAsync();

        // Fires while the key is still down, so the jump shows before the user lets go.
        private async Task JumpIfHeldAsync(String actionParameter, Int64 pressId, FolderSlotKind kind)
        {
            try
            {
                await Task.Delay(PluginTimings.HoldToJump).ConfigureAwait(false);
                var service = xkcdviewerPlugin.Instance?.Comics;
                if (service == null || !this._hold.TryHold(actionParameter, pressId))
                {
                    return;
                }

                await Jump(service, kind).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to handle holding '{actionParameter}'");
            }
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
