namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    // Draws the folder's keys. Every decision (what a key shows, where the comic goes, how a title card splits)
    // is made by tested helpers; this only turns those decisions into BitmapBuilder calls.
    internal static class KeyRenderer
    {
        // xkcd itself is black ink on white paper.
        private static readonly BitmapColor _paper = BitmapColor.White;
        private static readonly BitmapColor _ink = BitmapColor.Black;
        private static readonly BitmapColor _letterbox = BitmapColor.Black;
        private static readonly BitmapColor _arrow = BitmapColor.White;
        private static readonly BitmapColor _arrowDisabled = new BitmapColor(70, 70, 70);
        private static readonly BitmapColor _loading = new BitmapColor(255, 200, 0);
        private static readonly BitmapColor _offline = new BitmapColor(128, 128, 128);

        public static (Int32 Width, Int32 Height) TileSize(PluginImageSize imageSize)
        {
            using (var probe = new BitmapBuilder(imageSize))
            {
                return (probe.Width, probe.Height);
            }
        }

        // The key that opens a folder: the xkcdviewer logo, pre-scaled by art/make-icons.ps1 to the two key sizes seen on
        // hardware so it's never resampled on the device at those sizes. Any other size scales the nearest one. A label
        // ("latest", "random") goes in a strip along the bottom, so the three folder keys can be told apart; the logo
        // fills the key, so it's shrunk into the space above the strip.
        public static BitmapImage Logo(PluginImageSize imageSize, String label = null)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(_paper);
                var labelTop = builder.Height * 4 / 5;
                try
                {
                    using (var logo = PluginResources.ReadImage(builder.Width <= 80 ? "FolderButton80.png" : "FolderButton116.png"))
                    {
                        if (String.IsNullOrEmpty(label))
                        {
                            builder.DrawImage(logo, 0, 0, builder.Width, builder.Height);
                        }
                        else
                        {
                            var size = Math.Min(builder.Width, labelTop);
                            builder.DrawImage(logo, (builder.Width - size) / 2, 0, size, size);
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to draw the folder logo - showing the wordmark instead");
                    builder.DrawText("xkcd", _ink, builder.Height / 3);
                }

                if (!String.IsNullOrEmpty(label))
                {
                    builder.DrawText(label, 0, labelTop, builder.Width, builder.Height - labelTop, _ink, builder.Height / 7);
                }

                return builder.ToImage();
            }
        }

        // A padding key, or any key that has nothing to show. Plain black, like an unlit key.
        public static BitmapImage Blank(PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                return builder.ToImage();
            }
        }

        // A chevron drawn with lines rather than an arrow glyph, which the device's font may not have.
        public static BitmapImage Arrow(Boolean pointsRight, Boolean enabled, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                var color = enabled ? _arrow : _arrowDisabled;
                var width = builder.Width;
                var height = builder.Height;
                var stroke = Math.Max(3, width / 14);
                var tip = pointsRight ? width * 0.62f : width * 0.38f;
                var tail = pointsRight ? width * 0.40f : width * 0.60f;
                builder.DrawLine(tail, height * 0.28f, tip, height * 0.5f, color, stroke);
                builder.DrawLine(tip, height * 0.5f, tail, height * 0.72f, color, stroke);
                return builder.ToImage();
            }
        }

        // The Random folder's "show another" key: words, like the status tiles, rather than a symbol the device's font
        // may not have.
        public static BitmapImage ShowAnother(PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                builder.DrawText("show\nanother", _arrow, builder.Height / 6);
                return builder.ToImage();
            }
        }

        public static BitmapImage StatusTile(String text, Boolean loading, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                if (!String.IsNullOrEmpty(text))
                {
                    builder.DrawText(text, loading ? _loading : _offline, builder.Height / 6);
                }

                return builder.ToImage();
            }
        }

        public static BitmapImage TitleCardTile(String text, Int32 tileIndex, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(_paper);
                if (!String.IsNullOrEmpty(text))
                {
                    // The number tile is larger; title tiles fit TitleCardLayout's lines per tile.
                    var fontSize = tileIndex == 0 ? builder.Height / 4 : builder.Height / (TitleCardLayout.LinesPerTile + 2);
                    builder.DrawText(text, _ink, fontSize);
                }

                return builder.ToImage();
            }
        }

        // The whole 3x2 comic area as one picture, letterboxed per ComicFit. Null when the comic should get the title
        // card instead: its shape doesn't fit, or the bytes aren't an image the SDK can decode.
        public static BitmapImage BuildComicArea(Int32 num, Byte[] imageBytes, Int32 tileWidth, Int32 tileHeight, IPluginLog log)
        {
            var areaWidth = tileWidth * FolderLayout.ComicColumns;
            var areaHeight = tileHeight * FolderLayout.ComicRows;

            try
            {
                using (var comic = BitmapImage.FromArray(imageBytes))
                {
                    var placement = ComicFit.Place(comic.Width, comic.Height, areaWidth, areaHeight);
                    if (placement.Display == ComicDisplay.TitleCard)
                    {
                        log.Info($"Comic {num} is {comic.Width}x{comic.Height}, too large or too thin for the keys - showing its title card");
                        return null;
                    }

                    using (var builder = new BitmapBuilder(areaWidth, areaHeight))
                    {
                        builder.Clear(_letterbox);

                        // Some comics have transparent backgrounds, which need paper behind them.
                        builder.FillRectangle(placement.X, placement.Y, placement.Width, placement.Height, _paper);
                        builder.DrawImage(comic, placement.X, placement.Y, placement.Width, placement.Height);
                        return builder.ToImage();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Failed to draw comic {num}: {ex.Message}");
                return null;
            }
        }

        // A tile is the area drawn at a negative offset into a key-sized image, the same geometry as cropping
        // (hardware spike) without building an area-sized image per key.
        public static BitmapImage ComicTile(BitmapImage area, Int32 tileIndex, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                var (x, y) = FolderLayout.GetTileOffset(tileIndex, builder.Width, builder.Height);
                builder.DrawImage(area, -x, -y);
                return builder.ToImage();
            }
        }
    }
}
