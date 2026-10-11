namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public enum FolderSlotKind
    {
        Previous,
        Next,
        Padding,
        ComicTile,
    }

    // One key of the comic folder. ActionName is what the folder hands the SDK, and what comes back in
    // RunCommand and GetCommandImage.
    public sealed record FolderSlot(FolderSlotKind Kind, Int32 Index)
    {
        private const String PreviousName = "prev";
        private const String NextName = "next";
        private const String PaddingPrefix = "pad";
        private const String ComicTilePrefix = "tile";

        public static FolderSlot Previous { get; } = new FolderSlot(FolderSlotKind.Previous, 0);

        public static FolderSlot Next { get; } = new FolderSlot(FolderSlotKind.Next, 0);

        public String ActionName => this.Kind switch
        {
            FolderSlotKind.Previous => PreviousName,
            FolderSlotKind.Next => NextName,
            FolderSlotKind.Padding => PaddingPrefix + this.Index.ToString(CultureInfo.InvariantCulture),
            _ => ComicTilePrefix + this.Index.ToString(CultureInfo.InvariantCulture),
        };

        public static FolderSlot Padding(Int32 index) => new FolderSlot(FolderSlotKind.Padding, index);

        public static FolderSlot ComicTile(Int32 index) => new FolderSlot(FolderSlotKind.ComicTile, index);

        public static Boolean TryParse(String actionName, out FolderSlot slot)
        {
            slot = actionName switch
            {
                PreviousName => Previous,
                NextName => Next,
                _ when TryParseIndex(actionName, PaddingPrefix, out var index) => Padding(index),
                _ when TryParseIndex(actionName, ComicTilePrefix, out var index) && index < FolderLayout.ComicTileCount => ComicTile(index),
                _ => null,
            };

            return slot != null;
        }

        // The round trip rejects spellings that parse to a number but that we never hand out, such as "tile01".
        private static Boolean TryParseIndex(String actionName, String prefix, out Int32 index)
        {
            index = 0;
            return actionName != null
                && actionName.StartsWith(prefix, StringComparison.Ordinal)
                && Int32.TryParse(actionName.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out index)
                && actionName == prefix + index.ToString(CultureInfo.InvariantCulture);
        }
    }

    // Lays out the comic folder: Prev and Next on the top row, and the 3x2 comic block centred on the two rows
    // below, padded either side on grids wider than three keys. See docs/ai/product.md, "Layout".
    public static class FolderLayout
    {
        public const Int32 Rows = 3;
        public const Int32 ComicColumns = 3;
        public const Int32 ComicRows = Rows - 1;
        public const Int32 ComicTileCount = ComicColumns * ComicRows;

        // The SDK puts its Back key in grid slot 0 and the names we return fill the rest, left to right and top to
        // bottom. Exactly columns x rows - 1 names: one more and the SDK silently starts a second page.
        public static IReadOnlyList<FolderSlot> BuildSlots(Int32 columns)
        {
            if (columns < ComicColumns)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, $"The comic block needs at least {ComicColumns} columns");
            }

            var slots = new List<FolderSlot>(columns * Rows - 1) { FolderSlot.Previous, FolderSlot.Next };
            var paddingCount = 0;

            while (slots.Count < columns - 1)
            {
                slots.Add(FolderSlot.Padding(paddingCount++));
            }

            var left = (columns - ComicColumns) / 2;
            for (var row = 0; row < ComicRows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var comicColumn = column - left;
                    slots.Add(comicColumn >= 0 && comicColumn < ComicColumns
                        ? FolderSlot.ComicTile(row * ComicColumns + comicColumn)
                        : FolderSlot.Padding(paddingCount++));
                }
            }

            return slots;
        }

        // Where a tile's crop starts within the picture that spans the whole comic block.
        public static (Int32 X, Int32 Y) GetTileOffset(Int32 tileIndex, Int32 tileWidth, Int32 tileHeight) =>
            tileIndex < 0 || tileIndex >= ComicTileCount
                ? throw new ArgumentOutOfRangeException(nameof(tileIndex), tileIndex, $"Comic tiles are numbered 0 to {ComicTileCount - 1}")
                : (tileIndex % ComicColumns * tileWidth, tileIndex / ComicColumns * tileHeight);
    }
}
