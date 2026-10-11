namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    // A clock that only moves when the test says so.
    internal sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider()
            : this(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero))
        {
        }

        public ManualTimeProvider(DateTimeOffset start) => this._now = start;

        public override DateTimeOffset GetUtcNow() => this._now;

        public void Advance(TimeSpan by) => this._now += by;
    }
}
