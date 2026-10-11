namespace Loupedeck.xkcdPlugin.Tests
{
    using System;

    using Xunit;

    public class ImageCacheTests
    {
        [Fact]
        public void TryGet_AfterAdd_ReturnsTheImage()
        {
            var cache = new ImageCache(5, 1000);
            var image = new Byte[10];

            cache.Add(353, image);

            Assert.True(cache.TryGet(353, out var cached));
            Assert.Same(image, cached);
            Assert.True(cache.Contains(353));
        }

        [Fact]
        public void TryGet_NeverAdded_ReturnsFalse()
        {
            var cache = new ImageCache(5, 1000);

            Assert.False(cache.TryGet(353, out var cached));
            Assert.Null(cached);
            Assert.False(cache.Contains(353));
        }

        [Fact]
        public void Add_OverTheCountLimit_EvictsTheLeastRecentlyUsed()
        {
            var cache = new ImageCache(2, 1000);
            cache.Add(1, new Byte[10]);
            cache.Add(2, new Byte[10]);

            cache.Add(3, new Byte[10]);

            Assert.False(cache.Contains(1));
            Assert.True(cache.Contains(2));
            Assert.True(cache.Contains(3));
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void Add_OverTheByteLimit_EvictsUntilItFits()
        {
            var cache = new ImageCache(10, 100);
            cache.Add(1, new Byte[40]);
            cache.Add(2, new Byte[40]);

            cache.Add(3, new Byte[90]);

            Assert.False(cache.Contains(1));
            Assert.False(cache.Contains(2));
            Assert.True(cache.Contains(3));
            Assert.Equal(90, cache.TotalBytes);
        }

        [Fact]
        public void TryGet_RecentlyRead_IsKeptOverAnOlderImage()
        {
            var cache = new ImageCache(2, 1000);
            cache.Add(1, new Byte[10]);
            cache.Add(2, new Byte[10]);

            cache.TryGet(1, out _);
            cache.Add(3, new Byte[10]);

            Assert.True(cache.Contains(1));
            Assert.False(cache.Contains(2));
        }

        [Fact]
        public void Add_SameComicAgain_ReplacesItAndKeepsTheTotalRight()
        {
            var cache = new ImageCache(5, 1000);
            cache.Add(1, new Byte[10]);

            cache.Add(1, new Byte[30]);

            Assert.Equal(1, cache.Count);
            Assert.Equal(30, cache.TotalBytes);
        }

        [Fact]
        public void Add_BiggerThanTheWholeBudget_IsNotStoredAndEvictsNothing()
        {
            var cache = new ImageCache(5, 100);
            cache.Add(1, new Byte[50]);

            cache.Add(2, new Byte[101]);

            Assert.False(cache.Contains(2));
            Assert.True(cache.Contains(1));
            Assert.Equal(50, cache.TotalBytes);
        }

        [Fact]
        public void Add_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ImageCache(5, 100).Add(1, null));
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(5, 0)]
        public void Constructor_NonPositiveLimit_Throws(Int32 maxCount, Int64 maxBytes)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ImageCache(maxCount, maxBytes));
        }
    }
}
