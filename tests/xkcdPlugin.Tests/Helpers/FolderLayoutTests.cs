namespace Loupedeck.xkcdPlugin.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Xunit;

    public class FolderLayoutTests
    {
        // Each grid is written out as on the device, with "Back" for the SDK's own key in slot 0. Matches the
        // diagrams in docs/ai/product.md, "Layout".
        [Theory]
        [InlineData(3, "Back prev next | tile0 tile1 tile2 | tile3 tile4 tile5")]
        [InlineData(4, "Back prev next pad0 | tile0 tile1 tile2 pad1 | tile3 tile4 tile5 pad2")]
        [InlineData(5, "Back prev next pad0 pad1 | pad2 tile0 tile1 tile2 pad3 | pad4 tile3 tile4 tile5 pad5")]
        public void BuildSlots_KnownWidth_MatchesTheDocumentedLayout(Int32 columns, String expected)
        {
            var names = FolderLayout.BuildSlots(columns).Select(slot => slot.ActionName);

            Assert.Equal(expected, Render(names, columns));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(8)]
        public void BuildSlots_AnyWidth_FillsExactlyOnePageAfterTheBackKey(Int32 columns)
        {
            var slots = FolderLayout.BuildSlots(columns);

            Assert.Equal(columns * FolderLayout.Rows - 1, slots.Count);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(8)]
        public void BuildSlots_AnyWidth_HasUniqueNamesAndEveryTileOnce(Int32 columns)
        {
            var slots = FolderLayout.BuildSlots(columns);

            Assert.Equal(slots.Count, slots.Select(slot => slot.ActionName).Distinct().Count());
            Assert.Equal(
                Enumerable.Range(0, FolderLayout.ComicTileCount),
                slots.Where(slot => slot.Kind == FolderSlotKind.ComicTile).Select(slot => slot.Index));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(0)]
        [InlineData(-1)]
        public void BuildSlots_TooNarrowForTheComic_Throws(Int32 columns) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => FolderLayout.BuildSlots(columns));

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(2, 232, 0)]
        [InlineData(3, 0, 116)]
        [InlineData(5, 232, 116)]
        public void GetTileOffset_Tile_IsItsPlaceInTheBlock(Int32 tileIndex, Int32 expectedX, Int32 expectedY)
        {
            var (x, y) = FolderLayout.GetTileOffset(tileIndex, 116, 116);

            Assert.Equal(expectedX, x);
            Assert.Equal(expectedY, y);
        }

        [Fact]
        public void GetTileOffset_NonSquareTiles_UsesWidthAcrossAndHeightDown()
        {
            Assert.Equal((80, 60), FolderLayout.GetTileOffset(4, 80, 60));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public void GetTileOffset_NotATile_Throws(Int32 tileIndex) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => FolderLayout.GetTileOffset(tileIndex, 116, 116));

        [Fact]
        public void TryParse_EveryNameBuildSlotsHandsOut_RoundTrips()
        {
            foreach (var slot in FolderLayout.BuildSlots(5))
            {
                Assert.True(FolderSlot.TryParse(slot.ActionName, out var parsed));
                Assert.Equal(slot, parsed);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Prev")]
        [InlineData("tile")]
        [InlineData("tile6")]
        [InlineData("tile-1")]
        [InlineData("tile+1")]
        [InlineData("tile01")]
        [InlineData("tile 1")]
        [InlineData("tile1 ")]
        [InlineData("pad")]
        [InlineData("pad99999999999")]
        [InlineData("tile٣")]
        public void TryParse_NameWeNeverHandOut_IsRejected(String actionName)
        {
            Assert.False(FolderSlot.TryParse(actionName, out var slot));
            Assert.Null(slot);
        }

        private static String Render(IEnumerable<String> names, Int32 columns) =>
            String.Join(" | ", new[] { "Back" }.Concat(names)
                .Select((name, i) => (name, row: i / columns))
                .GroupBy(key => key.row, key => key.name)
                .Select(row => String.Join(" ", row)));
    }
}
