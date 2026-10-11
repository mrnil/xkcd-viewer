namespace Loupedeck.xkcdPlugin.Tests
{
    using System;

    // A clock that only moves when the test says so.
    internal sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this._now;

        public void Advance(TimeSpan by) => this._now += by;
    }
}
