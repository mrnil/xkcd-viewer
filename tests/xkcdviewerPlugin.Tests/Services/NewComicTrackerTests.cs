namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Moq;

    using Xunit;

    public class NewComicTrackerTests
    {
        private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan FlashInterval = TimeSpan.FromMilliseconds(500);

        private readonly Mock<IPluginLog> _mockLog = new Mock<IPluginLog>();
        private readonly ManualTimeProvider _time = new ManualTimeProvider();

        [Fact]
        public void FirstRun_TheLatestCountsAsShown_SoNothingIsUnread()
        {
            var tracker = this.Create(null, null);

            tracker.OnLatestKnown(3309);

            Assert.Equal(3309, tracker.NewestShown);
            Assert.False(tracker.HasUnread);
            Assert.Null(tracker.FirstUnread);
            Assert.Equal(NewComicLook.Normal, tracker.Look);
            Assert.False(tracker.IsFlashing);
        }

        [Fact]
        public void ANewComicFound_IsUnread_AndFlashes()
        {
            var tracker = this.Create(3309, 3309);

            tracker.OnLatestKnown(3310);

            Assert.True(tracker.HasUnread);
            Assert.Equal(3310, tracker.FirstUnread);
            Assert.True(tracker.IsFlashing);
        }

        [Fact]
        public void TheFlash_AlternatesGreenAndNormal_ThenStaysGreen()
        {
            var tracker = this.Create(3309, 3309);
            tracker.OnLatestKnown(3310);

            Assert.Equal(NewComicLook.Green, tracker.Look);
            this._time.Advance(FlashInterval);
            Assert.Equal(NewComicLook.Normal, tracker.Look);
            this._time.Advance(FlashInterval);
            Assert.Equal(NewComicLook.Green, tracker.Look);

            this._time.Advance(FlashDuration);
            Assert.False(tracker.IsFlashing);
            Assert.Equal(NewComicLook.Green, tracker.Look);
            this._time.Advance(FlashInterval);
            Assert.Equal(NewComicLook.Green, tracker.Look);
        }

        [Fact]
        public void AfterARestart_AComicStillUnread_IsGreenWithoutFlashing()
        {
            var tracker = this.Create(3309, 3310);

            Assert.True(tracker.HasUnread);
            Assert.Equal(NewComicLook.Green, tracker.Look);

            tracker.OnLatestKnown(3310);

            Assert.False(tracker.IsFlashing);
            Assert.Equal(NewComicLook.Green, tracker.Look);
        }

        [Fact]
        public void AfterARestart_AComicNewerThanLastTime_Flashes()
        {
            var tracker = this.Create(3309, 3310);

            tracker.OnLatestKnown(3311);

            Assert.True(tracker.IsFlashing);
            Assert.Equal(3310, tracker.FirstUnread);
        }

        [Fact]
        public void ALatestAlreadyShown_IsNotNew()
        {
            var tracker = this.Create(3310, 3309);

            tracker.OnLatestKnown(3310);

            Assert.False(tracker.HasUnread);
            Assert.False(tracker.IsFlashing);
        }

        [Fact]
        public void SeveralUnread_FirstUnreadIsTheOneAfterTheNewestShown_UntilTheLatestIsShown()
        {
            var tracker = this.Create(3309, 3309);
            tracker.OnLatestKnown(3311);

            tracker.MarkShown(3310);
            Assert.True(tracker.HasUnread);
            Assert.Equal(3311, tracker.FirstUnread);

            tracker.MarkShown(3311);
            Assert.False(tracker.HasUnread);
            Assert.Null(tracker.FirstUnread);
            Assert.Equal(NewComicLook.Normal, tracker.Look);
        }

        [Fact]
        public void ShowingTheNewComic_EndsTheFlashAtOnce()
        {
            var tracker = this.Create(3309, 3309);
            tracker.OnLatestKnown(3310);

            tracker.MarkShown(3310);

            Assert.False(tracker.IsFlashing);
            Assert.Equal(NewComicLook.Normal, tracker.Look);
        }

        [Fact]
        public void ShowingAnOlderComic_DoesNotMoveTheNewestShownBack()
        {
            var tracker = this.Create(3309, 3310);

            tracker.MarkShown(100);

            Assert.Equal(3309, tracker.NewestShown);
            Assert.Equal(3310, tracker.FirstUnread);
        }

        [Fact]
        public void TakeComicToOpen_WithOneUnread_GivesIt_AndTheKeyGoesBackToNormal()
        {
            var tracker = this.Create(3309, 3309);
            tracker.OnLatestKnown(3310);

            Assert.Equal(3310, tracker.TakeComicToOpen());

            Assert.False(tracker.HasUnread);
            Assert.False(tracker.IsFlashing);
            Assert.Equal(NewComicLook.Normal, tracker.Look);
            Assert.Equal(3310, tracker.NewestShown);
        }

        [Fact]
        public void TakeComicToOpen_WithSeveralUnread_GivesEachInTurn_StayingGreenUntilTheLast()
        {
            var tracker = this.Create(3309, 3309);
            tracker.OnLatestKnown(3311);

            Assert.Equal(3310, tracker.TakeComicToOpen());
            Assert.True(tracker.HasUnread);

            Assert.Equal(3311, tracker.TakeComicToOpen());
            Assert.False(tracker.HasUnread);
        }

        [Fact]
        public void TakeComicToOpen_WithNothingUnread_GivesTheLatest_WithoutAChange()
        {
            var tracker = this.Create(3310, 3310);
            var raised = 0;
            tracker.Changed += (_, _) => raised++;

            Assert.Equal(3310, tracker.TakeComicToOpen());
            Assert.Equal(0, raised);
        }

        [Fact]
        public void TakeComicToOpen_WithNothingKnown_GivesNull()
        {
            var tracker = this.Create(null, null);

            Assert.Null(tracker.TakeComicToOpen());
        }

        [Fact]
        public void TakeComicToOpen_RaisesChanged_SoTheNumbersAreSavedAndTheKeysRedrawn()
        {
            var tracker = this.Create(3309, 3310);
            var raised = 0;
            tracker.Changed += (_, _) => raised++;

            tracker.TakeComicToOpen();

            Assert.Equal(1, raised);
        }

        [Fact]
        public void AnOlderLatest_IsIgnored()
        {
            var tracker = this.Create(3309, 3309);
            tracker.OnLatestKnown(3311);

            tracker.OnLatestKnown(3310);

            Assert.Equal(3311, tracker.LatestKnown);
        }

        [Fact]
        public void Changed_IsRaisedForChanges_AndNotForRepeats()
        {
            var tracker = this.Create(3309, 3309);
            var raised = 0;
            tracker.Changed += (_, _) => raised++;

            tracker.OnLatestKnown(3310);
            tracker.OnLatestKnown(3310);
            tracker.MarkShown(3310);
            tracker.MarkShown(3310);

            Assert.Equal(2, raised);
        }

        [Fact]
        public void Changed_AHandlerFailing_IsLoggedAndTheOthersStillRun()
        {
            var tracker = this.Create(3309, 3309);
            var secondRan = false;
            tracker.Changed += (_, _) => throw new InvalidOperationException("boom");
            tracker.Changed += (_, _) => secondRan = true;

            tracker.OnLatestKnown(3310);

            Assert.True(secondRan);
            this._mockLog.Verify(x => x.Error(It.Is<String>(text => text.Contains("boom"))), Times.Once());
        }

        [Fact]
        public void SavedNumbersThatArentComics_AreIgnored()
        {
            var tracker = this.Create(0, -4);

            Assert.Null(tracker.NewestShown);
            Assert.Null(tracker.LatestKnown);
            Assert.False(tracker.HasUnread);
        }

        [Fact]
        public void Constructor_BadArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new NewComicTracker(null, 1, 1));
            Assert.Throws<ArgumentNullException>(() => new NewComicTracker(this._mockLog.Object, 1, 1, null, FlashDuration, FlashInterval));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NewComicTracker(this._mockLog.Object, 1, 1, this._time, FlashDuration, TimeSpan.Zero));
        }

        private NewComicTracker Create(Int32? newestShown, Int32? latestAtLastRun) =>
            new NewComicTracker(this._mockLog.Object, newestShown, latestAtLastRun, this._time, FlashDuration, FlashInterval);
    }
}
