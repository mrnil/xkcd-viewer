namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // THROWAWAY hardware spike (branch spike/keypad-grid, never merged).
    // Round 2 questions:
    //   1. What DeviceType does the service pass to GetButtonPressActionNames on each device?
    //   2. With a per-device grid (columns + whether the SDK Back key takes a grid slot), padded with blank
    //      keys, does the 3x2 comic block land in one piece on every device?
    //   3. Does an empty display name remove the caption the SDK draws under a folder key's image?
    //      (Comic tiles and pads return ""; the two control keys keep a caption for comparison.)
    // Watch the plugin log for lines starting "[spike]".
    public class GridSpikeFolder : PluginDynamicFolder
    {
        private const Int32 Rows = 3;
        private const Int32 ComicColumns = 3;
        private const String ModeKey = "mode";
        private const String RedrawKey = "redraw";
        private const String PadPrefix = "pad";

        private static readonly BitmapColor[] _backgrounds =
        {
            new BitmapColor(20, 40, 90),
            new BitmapColor(90, 30, 30),
            new BitmapColor(20, 80, 40),
        };

        // Observed on hardware in round 1. Anything else falls back to the keypad's 3x3.
        private static readonly Dictionary<DeviceType, GridProfile> _profiles = new Dictionary<DeviceType, GridProfile>
        {
            [DeviceType.Loupedeck70] = new GridProfile("MX Creative Keypad", 3, true),
            [DeviceType.Loupedeck50] = new GridProfile("Loupedeck Live S", 5, true),
            [DeviceType.Loupedeck20] = new GridProfile("Loupedeck CT", 4, false),
        };

        private static readonly GridProfile _fallback = new GridProfile("unknown (3x3 fallback)", 3, true);

        private readonly Object _lock = new Object();
        private readonly Dictionary<String, BitmapImage> _sources = new Dictionary<String, BitmapImage>();
        private readonly HashSet<PluginImageSize> _loggedSizes = new HashSet<PluginImageSize>();
        private readonly HashSet<String> _knownNames = new HashSet<String>();
        private SliceMode _mode;
        private Int32 _generation;

        public GridSpikeFolder()
        {
            this.DisplayName = "Grid Spike";
            this.GroupName = "Spike";
        }

        private enum SliceMode
        {
            OffsetNoGap,
            OffsetWithGap,
            CropNoGap,
        }

        public override Boolean Load()
        {
            PluginLog.Info("[spike] Loaded (round 2: device detection + caption test)");
            return true;
        }

        // Round 1 showed None does not remove the SDK Back key, so round 2 keeps the default and designs around it.
        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.ButtonArea;

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            var profile = _profiles.TryGetValue(deviceType, out var known) ? known : _fallback;
            var slots = BuildSlots(profile);

            lock (this._lock)
            {
                foreach (var slot in slots)
                {
                    this._knownNames.Add(slot);
                }
            }

            PluginLog.Info($"[spike] GetButtonPressActionNames: DeviceType={deviceType} (0x{(UInt32)deviceType:X}) -> {profile.Name}, "
                + $"{profile.Columns} columns, Back in grid: {profile.BackTakesGridSlot}. Slots: {String.Join(" ", slots)}");

            return slots.Select(this.CreateCommandName).ToList();
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
            => actionParameter == ModeKey || actionParameter == RedrawKey ? actionParameter : String.Empty;

        public override void RunCommand(String actionParameter)
        {
            PluginLog.Info($"[spike] Pressed '{actionParameter}'");

            if (actionParameter == ModeKey)
            {
                lock (this._lock)
                {
                    this._mode = (SliceMode)(((Int32)this._mode + 1) % 3);
                }

                this.RedrawAll();
            }
            else if (actionParameter == RedrawKey)
            {
                lock (this._lock)
                {
                    this._generation++;
                }

                this.RedrawAll();
            }
        }

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            try
            {
                lock (this._lock)
                {
                    var (width, height) = this.GetTileSize(imageSize);
                    return Int32.TryParse(actionParameter, out var tile)
                        ? this.DrawComicTile(tile, imageSize, width, height)
                        : actionParameter.StartsWith(PadPrefix, StringComparison.Ordinal)
                            ? DrawPad(imageSize)
                            : this.DrawControl(actionParameter, imageSize);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[spike] Drawing key '{actionParameter}' at {imageSize} failed");
                return null;
            }
        }

        // Row 0: [SDK Back, if it takes a grid slot] mode redraw, padded to the width.
        // Rows 1-2: the 3x2 comic block, centred, padded either side.
        private static List<String> BuildSlots(GridProfile profile)
        {
            var slots = new List<String>();
            var padCount = 0;
            String Pad() => $"{PadPrefix}{padCount++}";

            var firstRowOwn = profile.Columns - (profile.BackTakesGridSlot ? 1 : 0);
            slots.Add(ModeKey);
            slots.Add(RedrawKey);
            for (var i = 2; i < firstRowOwn; i++)
            {
                slots.Add(Pad());
            }

            var left = (profile.Columns - ComicColumns) / 2;
            for (var row = 0; row < Rows - 1; row++)
            {
                for (var column = 0; column < profile.Columns; column++)
                {
                    var comicColumn = column - left;
                    slots.Add(comicColumn >= 0 && comicColumn < ComicColumns
                        ? (row * ComicColumns + comicColumn).ToString()
                        : Pad());
                }
            }

            return slots;
        }

        private static BitmapImage DrawPad(PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                return builder.ToImage();
            }
        }

        private (Int32 Width, Int32 Height) GetTileSize(PluginImageSize imageSize)
        {
            using (var probe = new BitmapBuilder(imageSize))
            {
                if (this._loggedSizes.Add(imageSize))
                {
                    PluginLog.Info($"[spike] Tile size {imageSize} = {probe.Width}x{probe.Height} px");
                }

                return (probe.Width, probe.Height);
            }
        }

        private BitmapImage DrawComicTile(Int32 index, PluginImageSize imageSize, Int32 width, Int32 height)
        {
            var gap = this._mode == SliceMode.OffsetWithGap ? Math.Max(2, width / 8) : 0;
            var source = this.GetSource(imageSize, width, height, gap);
            var x = index % ComicColumns * (width + gap);
            var y = index / ComicColumns * (height + gap);

            BitmapImage slice;
            if (this._mode == SliceMode.CropNoGap)
            {
                using (var builder = new BitmapBuilder(source.Width, source.Height))
                {
                    builder.DrawImage(source, 0, 0);
                    builder.Crop(x, y, width, height);
                    slice = builder.ToImage();
                }
            }
            else
            {
                using (var builder = new BitmapBuilder(imageSize))
                {
                    builder.DrawImage(source, -x, -y);
                    slice = builder.ToImage();
                }
            }

            using (var labelled = new BitmapBuilder(imageSize))
            {
                labelled.DrawImage(slice, 0, 0);
                labelled.DrawText(index.ToString(), 0, 0, width / 3, height / 3, BitmapColor.White, height / 4);
                return labelled.ToImage();
            }
        }

        private BitmapImage DrawControl(String actionParameter, PluginImageSize imageSize)
        {
            var text = actionParameter == ModeKey
                ? $"mode\n{this._mode}"
                : $"redraw\n#{this._generation}";

            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                builder.DrawText(text, BitmapColor.White);
                return builder.ToImage();
            }
        }

        // One picture spanning all six comic tiles, built once per size/mode/generation. Everything in it
        // crosses the seams, so misaligned or scaled slices are obvious.
        private BitmapImage GetSource(PluginImageSize imageSize, Int32 width, Int32 height, Int32 gap)
        {
            var key = $"{imageSize}/{gap}/{this._generation}";
            if (this._sources.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var sourceWidth = ComicColumns * width + 2 * gap;
            var sourceHeight = 2 * height + gap;

            using (var builder = new BitmapBuilder(sourceWidth, sourceHeight))
            {
                builder.Clear(_backgrounds[this._generation % _backgrounds.Length]);

                var step = Math.Max(4, width / 4);
                for (var gridX = 0; gridX < sourceWidth; gridX += step)
                {
                    builder.DrawLine(gridX, 0, gridX, sourceHeight, new BitmapColor(255, 255, 255, 60), 1);
                }

                for (var gridY = 0; gridY < sourceHeight; gridY += step)
                {
                    builder.DrawLine(0, gridY, sourceWidth, gridY, new BitmapColor(255, 255, 255, 60), 1);
                }

                builder.DrawCircle(sourceWidth / 2f, sourceHeight / 2f, sourceHeight * 0.45f, BitmapColor.Red);
                builder.DrawLine(0, 0, sourceWidth, sourceHeight, BitmapColor.Green, 3);
                builder.DrawText("xkcd seam test", 0, sourceHeight / 3, sourceWidth, sourceHeight / 3, new BitmapColor(255, 220, 0), sourceHeight / 6);

                var image = builder.ToImage();
                this._sources[key] = image;
                PluginLog.Info($"[spike] Built source {sourceWidth}x{sourceHeight} px (gap {gap}) for {imageSize}");
                return image;
            }
        }

        private void RedrawAll()
        {
            String[] names;
            lock (this._lock)
            {
                names = this._knownNames.ToArray();
            }

            PluginLog.Info($"[spike] Redrawing {names.Length} keys (mode {this._mode}, generation {this._generation})");
            foreach (var name in names)
            {
                this.CommandImageChanged(name);
            }
        }

        private sealed class GridProfile
        {
            public GridProfile(String name, Int32 columns, Boolean backTakesGridSlot)
            {
                this.Name = name;
                this.Columns = columns;
                this.BackTakesGridSlot = backTakesGridSlot;
            }

            public String Name { get; }

            public Int32 Columns { get; }

            public Boolean BackTakesGridSlot { get; }
        }
    }
}
