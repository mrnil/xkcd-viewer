namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Xunit;

    public class ComicFitTests
    {
        // The comic area on the MX Creative Keypad: a 3x2 block of 116 px tiles.
        private const Int32 AreaWidth = 348;
        private const Int32 AreaHeight = 232;

        [Fact]
        public void Place_SameShapeAsTheArea_FillsIt()
        {
            var placement = ComicFit.Place(600, 400, AreaWidth, AreaHeight);

            Assert.Equal(new ComicPlacement(ComicDisplay.Picture, 0, 0, AreaWidth, AreaHeight), placement);
        }

        [Fact]
        public void Place_WideStrip_IsLetterboxedTopAndBottom()
        {
            // A typical four-panel strip.
            var placement = ComicFit.Place(740, 200, AreaWidth, AreaHeight);

            Assert.Equal(new ComicPlacement(ComicDisplay.Picture, 0, 69, AreaWidth, 94), placement);
        }

        [Fact]
        public void Place_SquareComic_IsLetterboxedLeftAndRight()
        {
            var placement = ComicFit.Place(500, 500, AreaWidth, AreaHeight);

            Assert.Equal(new ComicPlacement(ComicDisplay.Picture, 58, 0, AreaHeight, AreaHeight), placement);
        }

        [Fact]
        public void Place_SmallComic_IsScaledUpToFit()
        {
            var placement = ComicFit.Place(150, 100, AreaWidth, AreaHeight);

            Assert.Equal(new ComicPlacement(ComicDisplay.Picture, 0, 0, AreaWidth, AreaHeight), placement);
        }

        [Fact]
        public void Place_VeryTallComic_GetsTheTitleCard()
        {
            // Tall infographics like "Earth Temperature Timeline" would be a thin sliver in a landscape area.
            var placement = ComicFit.Place(740, 7000, AreaWidth, AreaHeight);

            Assert.Equal(new ComicPlacement(ComicDisplay.TitleCard, 0, 0, AreaWidth, AreaHeight), placement);
        }

        [Fact]
        public void Place_VeryWideComic_GetsTheTitleCard()
        {
            var placement = ComicFit.Place(2000, 150, AreaWidth, AreaHeight);

            Assert.Equal(ComicDisplay.TitleCard, placement.Display);
        }

        [Fact]
        public void Place_HugeComicOfAGoodShape_GetsTheTitleCard()
        {
            var placement = ComicFit.Place(3000, 2000, AreaWidth, AreaHeight);

            Assert.Equal(ComicDisplay.TitleCard, placement.Display);
        }

        [Theory]
        [InlineData(1740, 1160, ComicDisplay.Picture)]     // shrunk to exactly the minimum scale
        [InlineData(1741, 1161, ComicDisplay.TitleCard)]   // just under it
        public void Place_AroundTheMinimumScale_SwitchesJustBelowIt(Int32 width, Int32 height, ComicDisplay expected)
        {
            Assert.Equal(expected, ComicFit.Place(width, height, AreaWidth, AreaHeight).Display);
        }

        [Theory]
        [InlineData(87, 232, ComicDisplay.Picture)]        // covers exactly a quarter of the area
        [InlineData(86, 232, ComicDisplay.TitleCard)]      // just under it
        public void Place_AroundTheMinimumCoverage_SwitchesJustBelowIt(Int32 width, Int32 height, ComicDisplay expected)
        {
            Assert.Equal(expected, ComicFit.Place(width, height, AreaWidth, AreaHeight).Display);
        }

        [Fact]
        public void Place_SmallerKeys_ScalesToTheirArea()
        {
            // The CT's 80 px tiles.
            var placement = ComicFit.Place(740, 200, 240, 160);

            Assert.Equal(new ComicPlacement(ComicDisplay.Picture, 0, 47, 240, 65), placement);
        }

        [Theory]
        [InlineData(0, 400)]
        [InlineData(600, 0)]
        [InlineData(-600, 400)]
        public void Place_NonsenseImageSize_GetsTheTitleCard(Int32 width, Int32 height)
        {
            Assert.Equal(ComicDisplay.TitleCard, ComicFit.Place(width, height, AreaWidth, AreaHeight).Display);
        }

        [Fact]
        public void Place_ImageLargeEnoughToOverflowInt32Area_GetsTheTitleCard()
        {
            Assert.Equal(ComicDisplay.TitleCard, ComicFit.Place(Int32.MaxValue, Int32.MaxValue, AreaWidth, AreaHeight).Display);
        }

        [Theory]
        [InlineData(0, 232)]
        [InlineData(348, 0)]
        public void Place_EmptyArea_Throws(Int32 areaWidth, Int32 areaHeight) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => ComicFit.Place(600, 400, areaWidth, areaHeight));
    }
}
