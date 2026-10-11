namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Collections.Generic;

    // Least-recently-used cache of comic image bytes, limited by both count and total size. Not thread-safe:
    // ComicService only touches it under its own lock.
    internal sealed class ImageCache
    {
        private readonly Int32 _maxCount;
        private readonly Int64 _maxBytes;
        private readonly Dictionary<Int32, LinkedListNode<(Int32 Num, Byte[] Image)>> _entries = new Dictionary<Int32, LinkedListNode<(Int32 Num, Byte[] Image)>>();

        // Most recently used first.
        private readonly LinkedList<(Int32 Num, Byte[] Image)> _recency = new LinkedList<(Int32 Num, Byte[] Image)>();

        public ImageCache(Int32 maxCount, Int64 maxBytes)
        {
            if (maxCount < 1 || maxBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), $"Image cache limits must be positive, not {maxCount} images / {maxBytes} bytes");
            }

            this._maxCount = maxCount;
            this._maxBytes = maxBytes;
        }

        public Int32 Count => this._entries.Count;

        public Int64 TotalBytes { get; private set; }

        public Boolean Contains(Int32 num) => this._entries.ContainsKey(num);

        public Boolean TryGet(Int32 num, out Byte[] image)
        {
            if (!this._entries.TryGetValue(num, out var node))
            {
                image = null;
                return false;
            }

            this._recency.Remove(node);
            this._recency.AddFirst(node);
            image = node.Value.Image;
            return true;
        }

        // An image bigger than the whole budget isn't stored; it would only evict everything else and then itself.
        public void Add(Int32 num, Byte[] image)
        {
            ArgumentNullException.ThrowIfNull(image);

            this.Remove(num);
            if (image.Length > this._maxBytes)
            {
                return;
            }

            this._entries[num] = this._recency.AddFirst((num, image));
            this.TotalBytes += image.Length;

            while (this._entries.Count > this._maxCount || this.TotalBytes > this._maxBytes)
            {
                this.Remove(this._recency.Last.Value.Num);
            }
        }

        private void Remove(Int32 num)
        {
            if (this._entries.Remove(num, out var node))
            {
                this._recency.Remove(node);
                this.TotalBytes -= node.Value.Image.Length;
            }
        }
    }
}
