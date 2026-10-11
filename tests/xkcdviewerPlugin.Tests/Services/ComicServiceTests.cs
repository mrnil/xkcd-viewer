namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Collections.Concurrent;
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
        public async Task StartAsync_AtTheLatest_PreloadsOnlyTheTwoEarlierComics()
        {
            var service = this.CreateService(null);

            await service.StartAsync();

            foreach (var num in new[] { Latest - 1, Latest - 2 })
            {
                this.VerifyComicFetched(num, Times.Once());
                this.VerifyImageFetched(num, Times.Once());
            }

            this.VerifyComicFetched(Latest - 3, Times.Never());
            this.VerifyComicFetched(Latest + 1, Times.Never());
        }

        [Fact]
        public async Task StartAsync_NearTheFirstComic_PreloadsNothingBeforeIt()
        {
            var service = this.CreateService(2);

            await service.StartAsync();

            foreach (var num in new[] { 1, 3, 4 })
            {
                this.VerifyComicFetched(num, Times.Once());
            }

            this.VerifyComicFetched(0, Times.Never());
            this.VerifyComicFetched(5, Times.Never());
        }

        [Fact]
        public async Task StartAsync_LastViewedComic_ShowsItAndPreloadsTwoEitherSide()
        {
            var service = this.CreateService(353);

            await service.StartAsync();

            Assert.Equal(353, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
            Assert.True(service.View.CanGoNext);
            foreach (var num in new[] { 351, 352, 353, 354, 355 })
            {
                this.VerifyComicFetched(num, Times.Once());
                this.VerifyImageFetched(num, Times.Once());
            }

            this.VerifyComicFetched(350, Times.Never());
            this.VerifyComicFetched(356, Times.Never());
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
        public async Task NextAsync_AfterMoving_PreloadsOnlyTheComicNewlyInRange()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.NextAsync();

            this.VerifyComicFetched(356, Times.Once());
            this.VerifyImageFetched(356, Times.Once());
            this.VerifyComicFetched(355, Times.Once());
            this.VerifyComicFetched(357, Times.Never());
        }

        [Fact]
        public async Task PreviousAsync_RightAfterTwoNexts_FetchesNothingAgain()
        {
            var service = this.CreateService(353);
            await service.StartAsync();
            await service.NextAsync();
            await service.NextAsync();

            await service.PreviousAsync();
            await service.PreviousAsync();

            Assert.Equal(353, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
            foreach (var num in new[] { 351, 352, 353, 354, 355, 356, 357 })
            {
                this.VerifyImageFetched(num, Times.Once());
            }
        }

        [Fact]
        public void MaxCachedImages_IsThePreloadedWindowPlusOneSpareEachWay()
        {
            Assert.Equal(7, ComicService.MaxCachedImages);
        }

        [Fact]
        public async Task PreviousAsync_AfterMoving_ShowsItAndPreloadsTheComicNewlyInRange()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.PreviousAsync();

            Assert.Equal(352, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
            this.VerifyComicFetched(350, Times.Once());
            this.VerifyComicFetched(349, Times.Never());
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
            this.VerifyComicFetched(351, Times.Once());
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
            this.VerifyComicFetched(355, Times.Once());
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
        public async Task ShowLatestAsync_FromALastViewedComic_ShowsTheLatestAndPreloadsTheTwoBefore()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.ShowLatestAsync();

            var view = service.View;
            Assert.Equal(Latest, view.Num);
            Assert.Equal(ComicViewState.Ready, view.State);
            Assert.True(view.CanGoPrevious);
            Assert.False(view.CanGoNext);
            this.VerifyComicFetched(Latest - 2, Times.Once());
        }

        [Fact]
        public async Task ShowLatestAsync_BeforeTheLatestIsKnown_ShowsNoOtherComicThenTheLatest()
        {
            var release = new TaskCompletionSource<xkcdComic>(TaskCreationOptions.RunContinuationsAsynchronously);
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
            var service = this.CreateService(353);

            var showing = service.ShowLatestAsync();

            Assert.Null(service.View.Num);
            Assert.Equal(ComicViewState.Loading, service.View.State);
            release.SetResult(Comic(Latest));
            await showing;
            Assert.Equal(Latest, service.View.Num);
            this.VerifyComicFetched(353, Times.Never());
        }

        [Fact]
        public async Task ShowLatestAsync_LatestUnavailable_IsFailedWithNoComic()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);

            await service.ShowLatestAsync();

            Assert.Null(service.View.Num);
            Assert.Equal(ComicViewState.Failed, service.View.State);
        }

        [Fact]
        public async Task StartAsync_AfterShowLatestFailed_ShowsTheLastViewedComicAgain()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);
            await service.ShowLatestAsync();

            await service.StartAsync();

            Assert.Equal(353, service.View.Num);
        }

        [Fact]
        public async Task ShowLatestAsync_ComicPublishedSinceTheSessionStarted_ShowsTheNewOne()
        {
            this._mockClient.SetupSequence(x => x.GetLatestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Comic(Latest))
                .ReturnsAsync(Comic(Latest + 1));
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.ShowLatestAsync();

            Assert.Equal(Latest + 1, service.View.Num);
            Assert.False(service.View.CanGoNext);
            this.VerifyImageFetched(Latest + 1, Times.Once());
        }

        [Fact]
        public async Task NextAsync_AfterShowLatestFoundANewComic_StopsAtTheNewOne()
        {
            this._mockClient.SetupSequence(x => x.GetLatestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Comic(Latest))
                .ReturnsAsync(Comic(Latest + 1));
            var service = this.CreateService(Latest);
            await service.StartAsync();
            await service.ShowLatestAsync();
            await service.PreviousAsync();

            await service.NextAsync();
            await service.NextAsync();

            Assert.Equal(Latest + 1, service.View.Num);
        }

        [Fact]
        public async Task ShowLatestAsync_AskingAgainFails_ShowsTheLatestAlreadyKnown()
        {
            this._mockClient.SetupSequence(x => x.GetLatestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Comic(Latest))
                .ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.ShowLatestAsync();

            Assert.Equal(Latest, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
        }

        [Fact]
        public async Task ShowLatestAsync_SoonAfterAskingFailed_ShowsTheLatestAlreadyKnownWithoutAskingAgain()
        {
            this._mockClient.SetupSequence(x => x.GetLatestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Comic(Latest))
                .ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);
            await service.StartAsync();
            await service.ShowLatestAsync();
            await service.PreviousAsync();

            await service.ShowLatestAsync();

            Assert.Equal(Latest, service.View.Num);
            this._mockClient.Verify(x => x.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ShowLatestAsync_AnOlderAnswer_DoesNotPullTheLatestBack()
        {
            this._mockClient.SetupSequence(x => x.GetLatestAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Comic(Latest))
                .ReturnsAsync(Comic(Latest - 10));
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.ShowLatestAsync();

            Assert.Equal(Latest, service.View.Num);
        }

        [Fact]
        public async Task ShowFirstAsync_ShowsComicOneWithoutWaitingForTheLatest()
        {
            var release = new TaskCompletionSource<xkcdComic>(TaskCreationOptions.RunContinuationsAsynchronously);
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
            var service = this.CreateService(353);

            await service.ShowFirstAsync();

            var view = service.View;
            Assert.Equal(ComicNumbers.First, view.Num);
            Assert.Equal(ComicViewState.Ready, view.State);
            Assert.False(view.CanGoPrevious);
            this.VerifyComicFetched(ComicNumbers.First, Times.Once());
        }

        [Fact]
        public async Task ShowFirstAsync_WhileAJumpWaitsForTheLatest_WinsOverIt()
        {
            var release = new TaskCompletionSource<xkcdComic>(TaskCreationOptions.RunContinuationsAsynchronously);
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
            var service = this.CreateService(353);
            var showingLatest = service.ShowLatestAsync();

            await service.ShowFirstAsync();
            release.SetResult(Comic(Latest));
            await showingLatest;

            Assert.Equal(ComicNumbers.First, service.View.Num);
        }

        [Fact]
        public async Task ShowRandomAsync_ShowsTheDrawnComicAndPreloadsOnlyTheNextDraw()
        {
            var service = this.CreateService(null, Draws(100, 200));
            await service.ShowRandomAsync();

            var view = service.View;
            Assert.Equal(100, view.Num);
            Assert.Equal(ComicViewState.Ready, view.State);
            this.VerifyImageFetched(200, Times.Once());
            foreach (var neighbour in new[] { 98, 99, 101, 102 })
            {
                this.VerifyComicFetched(neighbour, Times.Never());
            }
        }

        [Fact]
        public async Task ShowRandomAsync_Again_ShowsThePreloadedComicAtOnce()
        {
            var service = this.CreateService(null, Draws(100, 200, 300));
            await service.ShowRandomAsync();
            ComicView seen = null;
            service.Changed += (sender, _) => seen ??= ((ComicService)sender).View;

            var showing = service.ShowRandomAsync();

            Assert.Equal(200, seen.Num);
            Assert.Equal(ComicViewState.Ready, seen.State);
            await showing;
            this.VerifyComicFetched(200, Times.Once());
            this.VerifyImageFetched(300, Times.Once());
        }

        [Fact]
        public async Task ShowRandomAsync_NeverShowsTheComicAlreadyShowing()
        {
            // Every draw is the lowest number allowed, so only the stepping over the current comic keeps it from repeating.
            var service = this.CreateService(1, (min, _) => min);
            await service.StartAsync();

            await service.ShowRandomAsync();

            Assert.NotEqual(1, service.View.Num);
        }

        [Fact]
        public async Task ShowRandomAsync_PicksBetweenTheFirstAndTheLatestComic()
        {
            var bounds = new ConcurrentBag<(Int32 Min, Int32 Max)>();
            var service = this.CreateService(null, (min, max) =>
            {
                bounds.Add((min, max));
                return min;
            });

            await service.ShowRandomAsync();

            // PickRandom asks for one number fewer than there are comics, as it steps over the one showing.
            Assert.All(bounds, range => Assert.True(range.Min >= ComicNumbers.First - 1 && range.Max <= Latest));
            Assert.InRange(service.View.Num.Value, ComicNumbers.First, Latest);
        }

        [Fact]
        public async Task PreviousAsync_AfterARandomComic_StepsBackAndPreloadsNeighboursAgain()
        {
            var service = this.CreateService(null, Draws(100, 200));
            await service.ShowRandomAsync();

            await service.PreviousAsync();

            Assert.Equal(99, service.View.Num);
            this.VerifyComicFetched(97, Times.Once());
        }

        [Fact]
        public async Task RefreshLatestAsync_ReturnsTheLatest_AndLeavesTheComicShowing()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            var latest = await service.RefreshLatestAsync();

            Assert.Equal(Comic(Latest), latest);
            Assert.Equal(353, service.View.Num);
        }

        [Fact]
        public async Task RefreshLatestAsync_AsksXkcdEveryTime()
        {
            var service = this.CreateService(353);

            await service.RefreshLatestAsync();
            await service.RefreshLatestAsync();

            this._mockClient.Verify(x => x.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RefreshLatestAsync_WhenAskingFails_ReturnsNull()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);

            Assert.Null(await service.RefreshLatestAsync());
        }

        [Fact]
        public async Task RefreshLatestAsync_AfterAnEarlierSuccess_StillReturnsNullWhenThisOneFails()
        {
            var service = this.CreateService(353);
            await service.RefreshLatestAsync();
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);

            Assert.Null(await service.RefreshLatestAsync());
        }

        [Fact]
        public async Task LatestKnown_IsRaisedWithTheLatestNumber_WhicheverWayItWasAskedFor()
        {
            var service = this.CreateService(353);
            var seen = new ConcurrentQueue<Int32>();
            service.LatestKnown += (_, latest) => seen.Enqueue(latest);

            await service.RefreshLatestAsync();
            await service.ShowLatestAsync();

            Assert.Equal(new[] { Latest, Latest }, seen);
        }

        [Fact]
        public async Task LatestKnown_IsNotRaisedWhenAskingFails()
        {
            this._mockClient.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync((xkcdComic)null);
            var service = this.CreateService(353);
            var raised = false;
            service.LatestKnown += (_, _) => raised = true;

            await service.RefreshLatestAsync();

            Assert.False(raised);
        }

        [Fact]
        public async Task ShowComicAsync_ShowsThatComic()
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.ShowComicAsync(Latest - 5);

            Assert.Equal(Latest - 5, service.View.Num);
            Assert.Equal(ComicViewState.Ready, service.View.State);
        }

        [Theory]
        [InlineData(Latest + 3, Latest)]
        [InlineData(0, 1)]
        public async Task ShowComicAsync_OutsideTheComics_StopsAtTheEnd(Int32 asked, Int32 shown)
        {
            var service = this.CreateService(353);
            await service.StartAsync();

            await service.ShowComicAsync(asked);

            Assert.Equal(shown, service.View.Num);
        }

        [Fact]
        public async Task ShowComicAsync_BeforeTheLatestIsKnown_DoesNothing()
        {
            var service = this.CreateService(353);

            await service.ShowComicAsync(400);

            Assert.Equal(353, service.View.Num);
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

        // A random source that turns into the given comic numbers, in order. They must be ascending: PickRandom then
        // adds one to every draw (stepping over the comic showing, or from the range starting at 0), so each draw is
        // the number minus one.
        private static Func<Int32, Int32, Int32> Draws(params Int32[] nums)
        {
            var next = 0;
            return (_, _) => nums[Interlocked.Increment(ref next) - 1] - 1;
        }

        private ComicService CreateService(Int32? startNum, Func<Int32, Int32, Int32> randomBetween = null) =>
            new ComicService(this._mockClient.Object, this._mockLog.Object, startNum, this._time, RetryDelay, randomBetween);

        private void VerifyComicFetched(Int32 num, Times times) =>
            this._mockClient.Verify(x => x.GetComicAsync(num, It.IsAny<CancellationToken>()), times);

        private void VerifyImageFetched(Int32 num, Times times) =>
            this._mockClient.Verify(x => x.GetImageAsync(Comic(num).ImageUrl, It.IsAny<CancellationToken>()), times);
    }
}
