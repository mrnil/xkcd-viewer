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

        // The new-comic green: "green" in xkcd's colour-name survey (xkcd.com itself has none). Owner's choice.
        private const String NewComicGreen = "#15B01A";
        private static readonly BitmapColor _newComicGreen = new BitmapColor(0x15, 0xB0, 0x1A);

        // The "new" banner on a green key is near-black, like the logo's ink, so it stands out from the green.
        private const String NewBannerFill = "#111111";
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

        // The logo's SVG source, read from the embedded resource on first use.
        private static readonly Lazy<String> _logoSvg = new Lazy<String>(() => PluginResources.ReadTextFile("xkcdviewerlogo.svg"));

        // The key that opens a folder: the xkcdviewer logo (art/xkcdviewerlogo.svg), drawn from the SVG at exactly the
        // size it's shown at, so it's sharp at every key size. A white margin of a tenth of the key all round keeps it
        // clear of the edge on every device. A label ("latest", "random") goes on a diagonal banner across the logo
        // (LogoBanner), so the three folder keys can be told apart while the logo stays the same size on all of them.
        //
        // VectorImage.ToBitmap renders an SVG at a given size. BitmapImage.FromSvg isn't used: drawn scaled into a
        // BitmapBuilder it came out blank (tested 2026-10-11), because it reports no size of its own.
        //
        // green draws the key on the new-comic green instead of white paper, for the New Comic key.
        public static BitmapImage Logo(PluginImageSize imageSize, String label = null, Boolean green = false)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(green ? _newComicGreen : _paper);
                try
                {
                    var margin = builder.Width / 10;
                    var size = Math.Min(builder.Width, builder.Height) - 2 * margin;
                    using (var logo = RenderSvg(_logoSvg.Value, size, size))
                    {
                        builder.DrawImage(logo, (builder.Width - size) / 2, (builder.Height - size) / 2);
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to draw the folder logo - showing the wordmark instead");
                    builder.DrawText("xkcd", _ink, builder.Height / 3);
                }

                if (!String.IsNullOrEmpty(label))
                {
                    try
                    {
                        using (var banner = RenderSvg(LogoBanner.Svg(label, green ? NewBannerFill : LogoBanner.DefaultFill), builder.Width, builder.Height))
                        {
                            builder.DrawImage(banner, 0, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"Failed to draw the '{label}' banner - showing the label as plain text instead");
                        builder.DrawText(label, 0, builder.Height * 4 / 5, builder.Width, builder.Height / 5, _ink, builder.Height / 7);
                    }
                }

                return builder.ToImage();
            }
        }

        private static BitmapImage RenderSvg(String svg, Int32 width, Int32 height)
        {
            var vector = new VectorImage();
            return vector.Load(svg) ? vector.ToBitmap(width, height) : throw new InvalidOperationException("The SVG didn't load");
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

        // A chevron, drawn from SVG (Chevron) rather than as an arrow glyph, which the device's font may not have. Its
        // character fallback only shows if the SVG fails to render.
        // green is the Comic Browser's Next chevron while a new comic is unread.
        public static BitmapImage Arrow(Boolean pointsRight, Boolean enabled, PluginImageSize imageSize, Boolean green = false)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                try
                {
                    using (var chevron = RenderSvg(Chevron.Svg(pointsRight, green ? NewComicGreen : enabled ? Chevron.Enabled : Chevron.Disabled), builder.Width, builder.Height))
                    {
                        builder.DrawImage(chevron, 0, 0);
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to draw a chevron - showing an arrow character instead");
                    builder.DrawText(pointsRight ? ">" : "<", enabled ? _arrow : _arrowDisabled, builder.Height / 2);
                }

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
