namespace Loupedeck.xkcdPlugin.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Moq;

    using Xunit;

    public class ComicServiceTests
    {
        private const Int32 Latest = 3000;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly Mock<IxkcdClient> _mockClient = new Mock<IxkcdClient>();
        private readonly Mock<IPluginLog> _mockLog = new Mock<IPluginLog>();
        private readonly ManualTimeProvider _time = new ManualTimeProvider();

        public ComicServiceTests()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Comic(Latest));
            this._mockClient.Setup(x => x.GetComicAsync(It.IsAny<Int32>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Int32 num, CancellationToken _) => Comic(num));
            this._mockClient.Setup(x => x.GetImageAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Uri url, CancellationToken _) => new Byte[] { 1, 2, 3 });
        }

        [Fact]
        public async Task StartAsync_NoLastViewedComic_ShowsTheLatest()
        {
            var service = this.CreateService(null);

            await service.StartAsync();

            var view = service.View;
            Assert.Equal(Latest, view.Num);
            Assert.Equal(ComicViewState.Ready, view.State);
            Assert.Equal(Comic(Latest), view.Comic);
            Assert.NotNull(view.Image);
            Assert.True(view.CanGoPrevious);
            Assert.False(view.CanGoNext);
        }

        [Fact]
        public async Task StartAsync_NoLastViewedComic_ReusesTheLatestComicsDetails()
        {
            var service = this.CreateService(null);

            await service.StartAsync();

            this.VerifyComicFetched(Latest, Times.Never());
            this.VerifyImageFetched(Latest, Times.Once());
        }

        [Fact]
        public async Task StartAsync_AtTheLatest_PreloadsOnlyThePreviousComic()
        {
            var service = this.CreateService(null);

            await service.StartAsync();

            this.VerifyComicFetched(Latest - 1, Times.Once());
            this.VerifyImageFetched(Latest - 1, Times.Once());
            this.VerifyComicFetched(Latest + 1, Times.Never());
        }

        [Fact]
        public async Task StartAsync_LastViewedComic_ShowsItAndPreloadsBothNeighbours()
        {
            var service = this.CreateService(353);

            await service.StartAsync();

            Assert.Equal(353, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
            Assert.True(service.View.CanGoNext);
            foreach (var num in new[] { 352, 353, 354 })
            {
                this.VerifyComicFetched(num, Times.Once());
                this.VerifyImageFetched(num, Times.Once());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task StartAsync_LastViewedIsNotAComicNumber_ShowsTheLatest(Int32 startNum)
        {
            var service = this.CreateService(startNum);

            await service.StartAsync();

            Assert.Equal(Latest, service.View.Num);
        }

        [Fact]
        public async Task StartAsync_LastViewedIsBeyondTheLatest_ShowsTheLatest()
        {
            var service = this.CreateService(Latest + 2000);

            await service.StartAsync();

            Assert.Equal(Latest, service.View.Num);
        }

        [Fact]
        public async Task StartAsync_CalledAgain_FetchesNothingNew()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.StartAsync();

            this._mockClient.Verify(x => x.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Once);
            this.VerifyComicFetched(353, Times.Once());
            this.VerifyImageFetched(353, Times.Once());
        }

        [Fact]
        public async Task StartAsync_ConcurrentCalls_FetchEachComicOnce()
        {
            var release = new TaskCompletionSource<xkcdComic>(TaskCreationOptions.RunContinuationsAsynchronously);
            this._mockClient.Setup(x => x.GetComicAsync(353, It.IsAny<CancellationToken>())).Returns(release.Task);
            var service = this.CreateService(353);

            var first = service.StartAsync();
            var second = service.StartAsync();
            Assert.Equal(ComicViewState.Loading, service.View.State);
            release.SetResult(Comic(353));
            await Task.WhenAll(first, second);

            this.VerifyComicFetched(353, Times.Once());
            Assert.Equal(ComicViewState.Ready, service.View.State);
        }

        [Fact]
        public async Task NextAsync_NeighbourPreloaded_IsReadyAtOnceWithoutFetchingItAgain()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            var moving = service.NextAsync();
            var view = service.View;
            await moving;

            Assert.Equal(354, view.Num);
            Assert.Equal(ComicViewState.Ready, view.State);
            this.VerifyComicFetched(354, Times.Once());
            this.VerifyImageFetched(354, Times.Once());
        }

        [Fact]
        public async Task NextAsync_AfterMoving_PreloadsTheNewNeighbour()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.NextAsync();

            this.VerifyComicFetched(355, Times.Once());
            this.VerifyImageFetched(355, Times.Once());
        }

        [Fact]
        public async Task PreviousAsync_AfterMoving_ShowsItAndPreloadsTheNewNeighbour()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.PreviousAsync();

            Assert.Equal(352, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
            this.VerifyComicFetched(351, Times.Once());
        }

        [Fact]
        public async Task PreviousAsync_AtTheFirstComic_DoesNothing()
        {
            var service = this.CreateService(1);
            await service.StartAsync();
            var changes = 0;
            service.Changed += (_, _) => changes++;

            await service.PreviousAsync();

            Assert.Equal(1, service.View.Num);
            Assert.False(service.View.CanGoPrevious);
            Assert.Equal(0, changes);
        }

        [Fact]
        public async Task NextAsync_AtTheLatest_DoesNothing()
        {
            var service = this.CreateService(null);
            await service.StartAsync();

            await service.NextAsync();

            Assert.Equal(Latest, service.View.Num);
        }

        [Fact]
        public async Task PreviousAsync_BeforeAnyComicIsKnown_DoesNothing()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(null);
            await service.StartAsync();

            await service.PreviousAsync();

            Assert.Null(service.View.Num);
        }

        [Fact]
        public async Task StartAsync_LatestUnavailableOnFirstRun_IsFailedWithNoComic()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(null);

            await service.StartAsync();

            Assert.Equal(new ComicView(null, ComicViewState.Failed, null, null, false, false), service.View);
        }

        [Fact]
        public async Task StartAsync_LatestUnavailable_StillShowsTheLastViewedComicButNotNext()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);

            await service.StartAsync();

            Assert.Equal(353, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
            Assert.False(service.View.CanGoNext);
            this.VerifyComicFetched(352, Times.Once());
            this.VerifyComicFetched(354, Times.Never());
        }

        [Fact]
        public async Task NextAsync_LatestUnknown_AsksForTheLatestAgainOnlyAfterTheDelay()
        {
            this._mockClient.SetupSequence(x => x.GetLatestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((xkcdComic)null)
                .ReturnsAsync(Comic(Latest));
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.NextAsync();
            this._mockClient.Verify(x => x.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Once);

            this._time.Advance(RetryDelay);
            await service.NextAsync();

            this._mockClient.Verify(x => x.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Equal(353, service.View.Num);
            Assert.True(service.View.CanGoNext);
            this.VerifyComicFetched(354, Times.Once());
        }

        [Fact]
        public async Task StartAsync_ComicUnavailable_IsFailedAndRetriedOnlyAfterTheDelay()
        {
            this._mockClient.SetupSequence(x => x.GetComicAsync(353, It.IsAny<CancellationToken>()))
                .ReturnsAsync((xkcdComic)null)
                .ReturnsAsync(Comic(353));
            var service = this.CreateService(353);

            await service.StartAsync();
            Assert.Equal(ComicViewState.Failed, service.View.State);

            await service.StartAsync();
            this.VerifyComicFetched(353, Times.Once());

            this._time.Advance(RetryDelay);
            await service.StartAsync();

            this.VerifyComicFetched(353, Times.Exactly(2));
            Assert.Equal(ComicViewState.Ready, service.View.State);
        }

        [Fact]
        public async Task StartAsync_ImageUnavailable_IsReadyWithoutAnImageAndRetriedOnlyAfterTheDelay()
        {
            var url = Comic(353).ImageUrl;
            this._mockClient.SetupSequence(x => x.GetImageAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Byte[])null)
                .ReturnsAsync(new Byte[] { 9 });
            var service = this.CreateService(353);

            await service.StartAsync();
            Assert.Equal(ComicViewState.Ready, service.View.State);
            Assert.Equal(Comic(353), service.View.Comic);
            Assert.Null(service.View.Image);

            await service.StartAsync();
            this.VerifyImageFetched(353, Times.Once());

            this._time.Advance(RetryDelay);
            await service.StartAsync();

            Assert.Equal(new Byte[] { 9 }, service.View.Image);
            this.VerifyComicFetched(353, Times.Once());
        }

        [Fact]
        public async Task StartAsync_ComicWithoutAnImage_IsReadyWithoutAskingForOne()
        {
            this._mockClient.Setup(x => x.GetComicAsync(1663, It.IsAny<CancellationToken>())).ReturnsAsync(new xkcdComic(1663, "Garden", null));
            var service = this.CreateService(1663);

            await service.StartAsync();

            Assert.Equal(new ComicView(1663, ComicViewState.Ready, new xkcdComic(1663, "Garden", null), null, true, true), service.View);
            this._mockClient.Verify(x => x.GetImageAsync(It.Is<Uri>(u => u.ToString().Contains("1663")), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_ClientThrows_IsFailedAndLogsTheComicAndTheError()
        {
            this._mockClient.Setup(x => x.GetComicAsync(353, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
            var service = this.CreateService(353);

            await service.StartAsync();

            Assert.Equal(ComicViewState.Failed, service.View.State);
            this._mockLog.Verify(x => x.Error(It.Is<String>(m => m.Contains("comic 353") && m.Contains("boom"))), Times.Once);
        }

        [Fact]
        public async Task StartAsync_ClientThrowsForTheLatest_IsFailedAndLogsTheError()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
            var service = this.CreateService(null);

            await service.StartAsync();

            Assert.Equal(ComicViewState.Failed, service.View.State);
            this._mockLog.Verify(x => x.Error(It.Is<String>(m => m.Contains("latest comic") && m.Contains("boom"))), Times.Once);
        }

        [Fact]
        public async Task View_WhileTheCurrentComicLoads_IsLoading()
        {
            var release = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            this._mockClient.Setup(x => x.GetImageAsync(Comic(353).ImageUrl, It.IsAny<CancellationToken>())).Returns(release.Task);
            var service = this.CreateService(353);

            var starting = service.StartAsync();
            await TestWait.UntilAsync(() => service.View.Comic != null, "the comic's details to arrive");

            Assert.Equal(ComicViewState.Loading, service.View.State);
            release.SetResult(new Byte[] { 1 });
            await starting;
            Assert.Equal(ComicViewState.Ready, service.View.State);
        }

        [Fact]
        public async Task Changed_CurrentComicFinishesLoading_IsRaised()
        {
            var service = this.CreateService(353);
            var changes = 0;
            service.Changed += (_, _) => Interlocked.Increment(ref changes);

            await service.StartAsync();

            Assert.True(changes > 0);
        }

        [Fact]
        public async Task Changed_OnMoving_IsRaisedBeforeTheNewComicLoads()
        {
            var service = this.CreateService(353);
            await service.StartAsync();
            ComicView seen = null;
            service.Changed += (sender, _) => seen ??= ((ComicService)sender).View;

            await service.NextAsync();

            Assert.Equal(354, seen.Num);
        }

        [Fact]
        public async Task Changed_HandlerThrows_OtherHandlersStillRunAndTheErrorIsLogged()
        {
            var service = this.CreateService(353);
            var secondRan = false;
            service.Changed += (_, _) => throw new InvalidOperationException("handler broke");
            service.Changed += (_, _) => secondRan = true;

            await service.StartAsync();

            Assert.True(secondRan);
            this._mockLog.Verify(x => x.Error(It.Is<String>(m => m.Contains("handler broke"))), Times.AtLeastOnce);
        }

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ComicService(null, this._mockLog.Object, null));
            Assert.Throws<ArgumentNullException>(() => new ComicService(this._mockClient.Object, null, null));
            Assert.Throws<ArgumentNullException>(() => new ComicService(this._mockClient.Object, this._mockLog.Object, null, null, RetryDelay));
        }

        [Fact]
        public void View_BeforeStarting_IsLoadingTheLastViewedComic()
        {
            var service = new ComicService(this._mockClient.Object, this._mockLog.Object, 353);

            Assert.Equal(353, service.View.Num);
            Assert.Equal(ComicViewState.Loading, service.View.State);
        }

        private static xkcdComic Comic(Int32 num) => new xkcdComic(num, $"Comic {num}", new Uri($"https://imgs.xkcd.com/comics/c{num}.png"));

        private ComicService CreateService(Int32? startNum) =>
            new ComicService(this._mockClient.Object, this._mockLog.Object, startNum, this._time, RetryDelay);

        private void VerifyComicFetched(Int32 num, Times times) =>
            this._mockClient.Verify(x => x.GetComicAsync(num, It.IsAny<CancellationToken>()), times);

        private void VerifyImageFetched(Int32 num, Times times) =>
            this._mockClient.Verify(x => x.GetImageAsync(Comic(num).ImageUrl, It.IsAny<CancellationToken>()), times);
    }
}
