namespace Loupedeck.xkcdPlugin.Tests
{
    using System;
    using System.Linq;

    using Xunit;

    public class TitleCardLayoutTests
    {
        [Fact]
        public void Layout_ShortTitle_PutsTheNumberFirstAndTheTitleNext()
        {
            var tiles = TitleCardLayout.Layout(1663, "Garden");

            Assert.Equal(new[] { "#1663", "Garden", "", "", "", "" }, tiles);
        }

        [Fact]
        public void Layout_LongerTitle_WrapsWordsAndMovesOnToTheNextTile()
        {
            var tiles = TitleCardLayout.Layout(1110, "Click and Drag across the whole wide map");

            Assert.Equal("#1110", tiles[0]);
            Assert.Equal("Click and\nDrag\nacross", tiles[1]);
            Assert.Equal("the whole\nwide map", tiles[2]);
            Assert.All(tiles.Skip(3), tile => Assert.Equal(String.Empty, tile));
        }

        [Fact]
        public void Layout_AnyTitle_NoLineIsWiderThanATileAllows()
        {
            var tiles = TitleCardLayout.Layout(1, "Supercalifragilisticexpialidocious is a very long word indeed and so is antidisestablishmentarianism");

            foreach (var line in tiles.SelectMany(tile => tile.Split('\n')))
            {
                Assert.True(line.Length <= TitleCardLayout.CharactersPerLine, $"'{line}' is too long");
            }
        }

        [Fact]
        public void Layout_WordLongerThanALine_IsHyphenated()
        {
            var tiles = TitleCardLayout.Layout(1, "Antidisestablishment");

            Assert.Equal("Antidise-\nstablish-\nment", tiles[1]);
        }

        [Fact]
        public void Layout_TitleTooLongForTheCard_EndsWithAnEllipsis()
        {
            var title = String.Join(" ", Enumerable.Repeat("word", 100));

            var tiles = TitleCardLayout.Layout(1, title);

            Assert.All(tiles.Skip(1), tile => Assert.Equal(TitleCardLayout.LinesPerTile, tile.Split('\n').Length));
            Assert.EndsWith("…", tiles[^1], StringComparison.Ordinal);
            Assert.True(tiles[^1].Split('\n')[^1].Length <= TitleCardLayout.CharactersPerLine);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Layout_NoTitle_ShowsOnlyTheNumber(String title)
        {
            Assert.Equal(new[] { "#7", "", "", "", "", "" }, TitleCardLayout.Layout(7, title));
        }

        [Fact]
        public void Layout_Always_ReturnsOneEntryPerTile()
        {
            Assert.Equal(FolderLayout.ComicTileCount, TitleCardLayout.Layout(1, "x").Length);
        }
    }
}
