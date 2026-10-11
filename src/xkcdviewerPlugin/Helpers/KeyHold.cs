namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Collections.Generic;

    // Tells a tap from a press and hold, per key. On each press the folder waits PluginTimings.HoldToJump and then
    // calls TryHold; whichever of TryHold and Release comes first decides what the press was. Pure state with no
    // timers, so it's tested without waiting.
    //
    // Thread-safe: button events and the hold delay arrive on different threads.
    public sealed class KeyHold
    {
        private readonly Object _lock = new Object();

        // Keys pressed and not yet released or held, with the press each one is waiting on.
        private readonly Dictionary<String, Int64> _waiting = new Dictionary<String, Int64>();
        private Int64 _lastPressId;

        // Returns the id to hand to TryHold once the hold delay is over.
        public Int64 Press(String key)
        {
            ArgumentNullException.ThrowIfNull(key);

            lock (this._lock)
            {
                var pressId = ++this._lastPressId;
                this._waiting[key] = pressId;
                return pressId;
            }
        }

        // True once, when this press is still held at the end of the delay: it's a hold. False if the key was
        // released, or pressed again since (an earlier press's delay ending late).
        public Boolean TryHold(String key, Int64 pressId)
        {
            ArgumentNullException.ThrowIfNull(key);

            lock (this._lock)
            {
                return this._waiting.TryGetValue(key, out var waitingFor) && waitingFor == pressId && this._waiting.Remove(key);
            }
        }

        // True when the press was a tap: released before it became a hold. False after a hold, or with no press.
        public Boolean Release(String key)
        {
            ArgumentNullException.ThrowIfNull(key);

            lock (this._lock)
            {
                return this._waiting.Remove(key);
            }
        }

        // Forget every press, e.g. when the folder closes with a key still down.
        public void Clear()
        {
            lock (this._lock)
            {
                this._waiting.Clear();
            }
        }
    }
}
