namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Collections.Generic;

    using Xunit;

    public class LruCacheTests
    {
        private readonly List<String> _evicted = new List<String>();

        [Fact]
        public void TryGet_AfterAdd_ReturnsTheValue()
        {
            var cache = this.Create(3);
            cache.Add(1, "one");

            Assert.True(cache.TryGet(1, out var value));
            Assert.Equal("one", value);
            Assert.False(cache.TryGet(2, out _));
        }

        [Fact]
        public void Add_OverCapacity_EvictsTheLeastRecentlyUsed()
        {
            var cache = this.Create(2);
            cache.Add(1, "one");
            cache.Add(2, "two");
            cache.TryGet(1, out _);

            cache.Add(3, "three");

            Assert.Equal(new[] { "two" }, this._evicted);
            Assert.True(cache.TryGet(1, out _));
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void Add_SameKey_EvictsTheOldValue()
        {
            var cache = this.Create(2);
            cache.Add(1, "old");

            cache.Add(1, "new");

            Assert.Equal(new[] { "old" }, this._evicted);
            Assert.True(cache.TryGet(1, out var value));
            Assert.Equal("new", value);
        }

        [Fact]
        public void Add_NullValue_IsCachedLikeAnyOther()
        {
            var cache = this.Create(2);

            cache.Add(1, null);

            Assert.True(cache.TryGet(1, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void Clear_EvictsEverything()
        {
            var cache = this.Create(3);
            cache.Add(1, "one");
            cache.Add(2, "two");

            cache.Clear();

            Assert.Equal(0, cache.Count);
            Assert.Equal(new[] { "two", "one" }, this._evicted);
        }

        [Fact]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<Int32, String>(0));
        }

        [Fact]
        public void Add_NoEvictionCallback_StillEvicts()
        {
            var cache = new LruCache<Int32, String>(1);
            cache.Add(1, "one");

            cache.Add(2, "two");

            Assert.False(cache.TryGet(1, out _));
        }

        private LruCache<Int32, String> Create(Int32 capacity) => new LruCache<Int32, String>(capacity, this._evicted.Add);
    }
}
