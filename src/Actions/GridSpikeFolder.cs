namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // THROWAWAY hardware spike (branch spike/keypad-grid, never merged) - answers the open questions in
    // docs/ai/product.md on a real keypad: does Navigation = None free key 0, does our own Back key work,
    // what order do the nine keys fill, what tile size is passed, how do seams look, does a full redraw tear.
    // Watch the plugin log for lines starting "[spike]".
    public class GridSpikeFolder : PluginDynamicFolder
    {
        private const Int32 ComicTileCount = 6;
        private const Int32 Columns = 3;
        private const String ModeKey = "6";
        private const String RedrawKey = "8";

        private static readonly BitmapColor[] _backgrounds =
        {
            new BitmapColor(20, 40, 90),
            new BitmapColor(90, 30, 30),
            new BitmapColor(20, 80, 40),
        };

        private readonly Object _lock = new Object();
        private readonly Dictionary<String, BitmapImage> _sources = new Dictionary<String, BitmapImage>();
        private readonly HashSet<PluginImageSize> _loggedSizes = new HashSet<PluginImageSize>();
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
            PluginLog.Info($"[spike] Loaded: NavigateUpActionName='{PluginDynamicFolder.NavigateUpActionName}'");
            return true;
        }

        // The Navigation property is obsolete ("override GetNavigationArea instead"). The device type is
        // ignored on purpose: the plugin always assumes a 3x3 grid (docs/ai/product.md).
        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.None;

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            var names = new List<String>();
            for (var index = 0; index < 9; index++)
            {
                names.Add(index == 7
                    ? PluginDynamicFolder.NavigateUpActionName
                    : this.CreateCommandName(index.ToString()));
            }

            PluginLog.Info($"[spike] Button names: {String.Join(" | ", names)}");
            return names;
        }

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
            if (!Int32.TryParse(actionParameter, out var index))
            {
                return null;
            }

            try
            {
                lock (this._lock)
                {
                    var (width, height) = this.GetTileSize(imageSize);
                    return index < ComicTileCount
                        ? this.DrawComicTile(index, imageSize, width, height)
                        : this.DrawControl(actionParameter, imageSize);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[spike] Drawing key '{actionParameter}' at {imageSize} failed");
                return null;
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
            var x = index % Columns * (width + gap);
            var y = index / Columns * (height + gap);

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
                ? $"6\nmode\n{this._mode}"
                : $"8\nredraw\n#{this._generation}";

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

            var sourceWidth = Columns * width + 2 * gap;
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
            PluginLog.Info($"[spike] Redrawing all keys (mode {this._mode}, generation {this._generation})");
            foreach (var index in Enumerable.Range(0, 9).Where(i => i != 7))
            {
                this.CommandImageChanged(index.ToString());
            }
        }
    }
}
