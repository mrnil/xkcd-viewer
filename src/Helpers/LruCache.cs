namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Collections.Generic;

    // A count-limited least-recently-used cache. Evicted values go to onEvicted (e.g. to dispose them). Not
    // thread-safe: callers lock around it.
    public sealed class LruCache<TKey, TValue>
    {
        private readonly Int32 _capacity;
        private readonly Action<TValue> _onEvicted;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _entries = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();

        // Most recently used first.
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _recency = new LinkedList<KeyValuePair<TKey, TValue>>();

        public LruCache(Int32 capacity, Action<TValue> onEvicted = null)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1");
            }

            this._capacity = capacity;
            this._onEvicted = onEvicted;
        }

        public Int32 Count => this._entries.Count;

        public Boolean TryGet(TKey key, out TValue value)
        {
            if (!this._entries.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }

            this._recency.Remove(node);
            this._recency.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        public void Add(TKey key, TValue value)
        {
            if (this._entries.Remove(key, out var existing))
            {
                this._recency.Remove(existing);
                this._onEvicted?.Invoke(existing.Value.Value);
            }

            this._entries[key] = this._recency.AddFirst(new KeyValuePair<TKey, TValue>(key, value));

            while (this._entries.Count > this._capacity)
            {
                var oldest = this._recency.Last;
                this._recency.RemoveLast();
                this._entries.Remove(oldest.Value.Key);
                this._onEvicted?.Invoke(oldest.Value.Value);
            }
        }

        public void Clear()
        {
            foreach (var entry in this._recency)
            {
                this._onEvicted?.Invoke(entry.Value);
            }

            this._recency.Clear();
            this._entries.Clear();
        }
    }
}
