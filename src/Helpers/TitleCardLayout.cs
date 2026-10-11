namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    // Splits a title card across the six comic tiles. Text crossing a seam between keys loses letters (hardware
    // spike), so every word stays inside one tile: the comic number gets tile 0, and the title fills tiles 1-5 in
    // reading order, word-wrapped to a fixed number of lines per tile.
    public static class TitleCardLayout
    {
        public const Int32 LinesPerTile = 3;
        public const Int32 CharactersPerLine = 9;

        private const Int32 TitleTiles = FolderLayout.ComicTileCount - 1;
        private const String Ellipsis = "…";

        // One entry per tile, each with its lines joined by '\n'. Unused tiles are empty.
        public static String[] Layout(Int32 num, String title)
        {
            var tiles = new String[FolderLayout.ComicTileCount];
            Array.Fill(tiles, String.Empty);
            tiles[0] = "#" + num.ToString(CultureInfo.InvariantCulture);

            var lines = WrapLines(title ?? String.Empty);
            var capacity = TitleTiles * LinesPerTile;
            if (lines.Count > capacity)
            {
                lines.RemoveRange(capacity, lines.Count - capacity);
                var last = lines[capacity - 1];
                lines[capacity - 1] = (last.Length < CharactersPerLine ? last : last[..(CharactersPerLine - 1)]) + Ellipsis;
            }

            for (var tile = 0; tile < TitleTiles; tile++)
            {
                var start = tile * LinesPerTile;
                if (start >= lines.Count)
                {
                    break;
                }

                tiles[tile + 1] = String.Join("\n", lines.GetRange(start, Math.Min(LinesPerTile, lines.Count - start)));
            }

            return tiles;
        }

        // Greedy word wrap. A word longer than a line is broken with a hyphen so it never overflows its tile.
        private static List<String> WrapLines(String text)
        {
            var lines = new List<String>();
            var line = new StringBuilder();

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var rest = word;
                while (rest.Length > CharactersPerLine)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }

                    lines.Add(rest[..(CharactersPerLine - 1)] + "-");
                    rest = rest[(CharactersPerLine - 1)..];
                }

                if (line.Length > 0 && line.Length + 1 + rest.Length > CharactersPerLine)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(rest);
            }

            if (line.Length > 0)
            {
                lines.Add(line.ToString());
            }

            return lines;
        }
    }
}
