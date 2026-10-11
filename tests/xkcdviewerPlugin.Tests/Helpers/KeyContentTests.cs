namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Xunit;

    public class KeyContentTests
    {
        private static readonly xkcdComic _python = new xkcdComic(353, "Python", new Uri("https://imgs.xkcd.com/comics/python.png"));

        private static readonly ComicView _ready = new ComicView(353, ComicViewState.Ready, _python, new Byte[] { 1 }, true, true);

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void For_Arrows_AreEnabledOnlyWhenThereIsSomewhereToGo(Boolean canGoPrevious, Boolean canGoNext)
        {
            var view = _ready with { CanGoPrevious = canGoPrevious, CanGoNext = canGoNext };

            Assert.Equal(new KeyContent(KeyContentKind.PreviousArrow, canGoPrevious), KeyContent.For(FolderSlot.Previous, view));
            Assert.Equal(new KeyContent(KeyContentKind.NextArrow, canGoNext), KeyContent.For(FolderSlot.Next, view));
        }

        [Theory]
        [InlineData(ComicFolderMode.Browse)]
        [InlineData(ComicFolderMode.Latest)]
        public void For_ArrowsOutsideTheRandomFolder_ArePrevAndNext(ComicFolderMode mode)
        {
            Assert.Equal(KeyContentKind.PreviousArrow, KeyContent.For(FolderSlot.Previous, _ready, mode).Kind);
            Assert.Equal(KeyContentKind.NextArrow, KeyContent.For(FolderSlot.Next, _ready, mode).Kind);
        }

        [Fact]
        public void For_ArrowsInTheRandomFolder_AreBothShowAnother()
        {
            var view = _ready with { CanGoPrevious = false, CanGoNext = false };

            Assert.Equal(new KeyContent(KeyContentKind.ShowAnother), KeyContent.For(FolderSlot.Previous, view, ComicFolderMode.Random));
            Assert.Equal(new KeyContent(KeyContentKind.ShowAnother), KeyContent.For(FolderSlot.Next, view, ComicFolderMode.Random));
        }

        [Fact]
        public void For_TilesInTheRandomFolder_AreTheComicAsInTheOtherFolders()
        {
            Assert.Equal(KeyContent.For(FolderSlot.ComicTile(4), _ready), KeyContent.For(FolderSlot.ComicTile(4), _ready, ComicFolderMode.Random));
            Assert.Equal(KeyContentKind.Blank, KeyContent.For(FolderSlot.Padding(0), _ready, ComicFolderMode.Random).Kind);
        }

        [Fact]
        public void For_Padding_IsBlank()
        {
            Assert.Equal(KeyContentKind.Blank, KeyContent.For(FolderSlot.Padding(0), _ready).Kind);
        }

        [Fact]
        public void For_TileOfAReadyComicWithAnImage_IsThatPartOfTheComic()
        {
            Assert.Equal(new KeyContent(KeyContentKind.ComicTile, TileIndex: 4), KeyContent.For(FolderSlot.ComicTile(4), _ready));
        }

        [Fact]
        public void For_TileOfAReadyComicWithoutAnImage_IsItsShareOfTheTitleCard()
        {
            var view = _ready with { Comic = new xkcdComic(1663, "Garden", null), Num = 1663, Image = null };

            Assert.Equal(new KeyContent(KeyContentKind.TitleCardTile, TileIndex: 0, Text: "#1663"), KeyContent.For(FolderSlot.ComicTile(0), view));
            Assert.Equal(new KeyContent(KeyContentKind.TitleCardTile, TileIndex: 1, Text: "Garden"), KeyContent.For(FolderSlot.ComicTile(1), view));
        }

        [Theory]
        [InlineData(1, "loading…")]
        [InlineData(4, "#353")]
        [InlineData(0, "")]
        [InlineData(5, "")]
        public void For_TileWhileLoading_ShowsStatusAndNumberOnSeparateTiles(Int32 tile, String expected)
        {
            var view = new ComicView(353, ComicViewState.Loading, null, null, true, true);

            Assert.Equal(new KeyContent(KeyContentKind.LoadingTile, TileIndex: tile, Text: expected), KeyContent.For(FolderSlot.ComicTile(tile), view));
        }

        [Theory]
        [InlineData(1, "offline")]
        [InlineData(4, "#353")]
        public void For_TileWhenFailed_ShowsOffline(Int32 tile, String expected)
        {
            var view = new ComicView(353, ComicViewState.Failed, null, null, true, false);

            Assert.Equal(new KeyContent(KeyContentKind.OfflineTile, TileIndex: tile, Text: expected), KeyContent.For(FolderSlot.ComicTile(tile), view));
        }

        [Fact]
        public void For_TileBeforeAnyComicIsKnown_HasNoNumber()
        {
            var view = new ComicView(null, ComicViewState.Failed, null, null, false, false);

            Assert.Equal(String.Empty, KeyContent.For(FolderSlot.ComicTile(KeyContent.NumberTile), view).Text);
            Assert.Equal("offline", KeyContent.For(FolderSlot.ComicTile(KeyContent.StatusTile), view).Text);
        }

        [Fact]
        public void For_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => KeyContent.For(null, _ready));
            Assert.Throws<ArgumentNullException>(() => KeyContent.For(FolderSlot.Next, null));
        }
    }
}
