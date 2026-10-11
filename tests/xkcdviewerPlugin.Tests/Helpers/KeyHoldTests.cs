namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Xunit;

    public class KeyHoldTests
    {
        private readonly KeyHold _hold = new KeyHold();

        [Fact]
        public void Release_BeforeTheDelayEnds_IsATapAndNotAHold()
        {
            var press = this._hold.Press("next");

            Assert.True(this._hold.Release("next"));
            Assert.False(this._hold.TryHold("next", press));
        }

        [Fact]
        public void TryHold_StillDownWhenTheDelayEnds_IsAHoldAndNotATap()
        {
            var press = this._hold.Press("next");

            Assert.True(this._hold.TryHold("next", press));
            Assert.False(this._hold.Release("next"));
        }

        [Fact]
        public void TryHold_OnlyOncePerPress()
        {
            var press = this._hold.Press("next");

            Assert.True(this._hold.TryHold("next", press));
            Assert.False(this._hold.TryHold("next", press));
        }

        [Fact]
        public void TryHold_AnEarlierPressesDelayEndingLate_IsNotAHold()
        {
            // Tap, then press again: the first press's delay ends while the second press is down.
            var first = this._hold.Press("next");
            this._hold.Release("next");
            var second = this._hold.Press("next");

            Assert.False(this._hold.TryHold("next", first));
            Assert.True(this._hold.TryHold("next", second));
        }

        [Fact]
        public void TryHoldNow_WhileDown_IsAHoldAtOnce_AndNotATap()
        {
            var press = this._hold.Press("next");

            Assert.True(this._hold.TryHoldNow("next"));
            Assert.False(this._hold.Release("next"));
            Assert.False(this._hold.TryHold("next", press));
        }

        [Fact]
        public void TryHoldNow_OnlyOncePerPress()
        {
            this._hold.Press("next");

            Assert.True(this._hold.TryHoldNow("next"));
            Assert.False(this._hold.TryHoldNow("next"));
        }

        [Fact]
        public void TryHoldNow_AfterARelease_IsNotAHold()
        {
            this._hold.Press("next");
            this._hold.Release("next");

            Assert.False(this._hold.TryHoldNow("next"));
        }

        [Fact]
        public void TryHoldNow_AfterTheDelayAlreadyMadeItAHold_IsNotASecondHold()
        {
            var press = this._hold.Press("next");
            this._hold.TryHold("next", press);

            Assert.False(this._hold.TryHoldNow("next"));
        }

        [Fact]
        public void Keys_AreTrackedSeparately()
        {
            var next = this._hold.Press("next");
            var previous = this._hold.Press("prev");

            Assert.True(this._hold.Release("prev"));
            Assert.True(this._hold.TryHold("next", next));
            Assert.False(this._hold.TryHold("prev", previous));
        }

        [Fact]
        public void Release_WithNoPress_IsNotATap()
        {
            Assert.False(this._hold.Release("next"));
        }

        [Fact]
        public void Clear_ForgetsKeysStillDown()
        {
            var press = this._hold.Press("next");

            this._hold.Clear();

            Assert.False(this._hold.TryHold("next", press));
            Assert.False(this._hold.Release("next"));
        }

        [Fact]
        public void NullKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => this._hold.Press(null));
            Assert.Throws<ArgumentNullException>(() => this._hold.TryHold(null, 1));
            Assert.Throws<ArgumentNullException>(() => this._hold.Release(null));
            Assert.Throws<ArgumentNullException>(() => this._hold.TryHoldNow(null));
        }
    }
}
