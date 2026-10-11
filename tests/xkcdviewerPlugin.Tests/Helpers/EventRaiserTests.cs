namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Collections.Generic;

    using Moq;

    using Xunit;

    public class EventRaiserTests
    {
        private readonly Mock<IPluginLog> _mockLog = new Mock<IPluginLog>();

        private event EventHandler Plain;

        private event EventHandler<Int32> WithArgs;

        [Fact]
        public void Raise_CallsEveryHandlerInOrderWithTheSender()
        {
            var calls = new List<String>();
            this.Plain += (sender, _) => calls.Add($"first:{sender}");
            this.Plain += (sender, _) => calls.Add($"second:{sender}");

            EventRaiser.Raise(this.Plain, "me", this._mockLog.Object, "Plain");

            Assert.Equal(new[] { "first:me", "second:me" }, calls);
            this._mockLog.Verify(x => x.Error(It.IsAny<String>()), Times.Never);
        }

        [Fact]
        public void Raise_AHandlerThrows_TheOthersStillRunAndTheEventAndErrorAreLogged()
        {
            var laterRan = false;
            this.Plain += (_, _) => throw new InvalidOperationException("boom");
            this.Plain += (_, _) => laterRan = true;

            EventRaiser.Raise(this.Plain, this, this._mockLog.Object, "Plain");

            Assert.True(laterRan);
            this._mockLog.Verify(x => x.Error(It.Is<String>(m => m.Contains("Plain") && m.Contains("boom"))), Times.Once);
        }

        [Fact]
        public void Raise_NoHandlers_DoesNothing()
        {
            EventRaiser.Raise(this.Plain, this, this._mockLog.Object, "Plain");
            EventRaiser.Raise(this.WithArgs, this, 7, this._mockLog.Object, "WithArgs");

            this._mockLog.VerifyNoOtherCalls();
        }

        [Fact]
        public void RaiseWithArgs_PassesTheArgsAndIsolatesAFailingHandler()
        {
            var seen = new List<Int32>();
            this.WithArgs += (_, _) => throw new InvalidOperationException("boom");
            this.WithArgs += (_, value) => seen.Add(value);

            EventRaiser.Raise(this.WithArgs, this, 3310, this._mockLog.Object, "WithArgs");

            Assert.Equal(new[] { 3310 }, seen);
            this._mockLog.Verify(x => x.Error(It.Is<String>(m => m.Contains("WithArgs") && m.Contains("boom"))), Times.Once);
        }

        [Fact]
        public void Raise_NoLog_StillSwallowsTheFailure()
        {
            var laterRan = false;
            this.Plain += (_, _) => throw new InvalidOperationException("boom");
            this.Plain += (_, _) => laterRan = true;

            EventRaiser.Raise(this.Plain, this, null, "Plain");

            Assert.True(laterRan);
        }
    }
}
