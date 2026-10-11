namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    using Moq;

    using Xunit;

    public class xkcdClientTests
    {
        private readonly Mock<IPluginLog> _mockLog = new Mock<IPluginLog>();

        [Fact]
        public async Task GetLatestAsync_RealResponse_RequestsTheLatestJsonAndReturnsTheComic()
        {
            var handler = StubHttpMessageHandler.Json(Fixture.Read("latest-3309.json"));

            var comic = await this.CreateClient(handler).GetLatestAsync();

            Assert.Equal("https://xkcd.com/info.0.json", Assert.Single(handler.Requests).RequestUri.ToString());
            Assert.Equal(new xkcdComic(3309, "Dogcatcher", new Uri("https://imgs.xkcd.com/comics/dogcatcher.png"), new DateOnly(2026, 10, 9)), comic);
        }

        [Fact]
        public async Task GetComicAsync_RealResponse_RequestsThatComicsJsonAndReturnsIt()
        {
            var handler = StubHttpMessageHandler.Json(Fixture.Read("353.json"));

            var comic = await this.CreateClient(handler).GetComicAsync(353);

            Assert.Equal("https://xkcd.com/353/info.0.json", Assert.Single(handler.Requests).RequestUri.ToString());
            Assert.Equal(353, comic.Num);
            Assert.Equal("Python", comic.Title);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetComicAsync_NoSuchComic_ReturnsNullWithoutARequest(Int32 num)
        {
            var handler = StubHttpMessageHandler.Json(Fixture.Read("353.json"));

            var comic = await this.CreateClient(handler).GetComicAsync(num);

            Assert.Null(comic);
            Assert.Empty(handler.Requests);
            this.VerifyWarning($"comic {num}");
        }

        [Fact]
        public async Task GetComicAsync_NotFound_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.NotFound, new StringContent("<html>404 Not Found</html>"));

            var comic = await this.CreateClient(handler).GetComicAsync(9999);

            Assert.Null(comic);
            this.VerifyWarning("comic 9999", "404");
        }

        [Fact]
        public async Task GetComicAsync_Comic404AnsweredWithHttp404_ReturnsTheJokeComic()
        {
            var handler = Respond(HttpStatusCode.NotFound, new StringContent("<html>404 Not Found</html>", null, "text/html"));

            var comic = await this.CreateClient(handler).GetComicAsync(404);

            Assert.Equal("https://xkcd.com/404/info.0.json", Assert.Single(handler.Requests).RequestUri.ToString());
            Assert.Equal(new xkcdComic(404, "404 Not Found", null), comic);
            Assert.Equal(new Uri("https://xkcd.com/404/"), comic.PageUrl);
            this._mockLog.Verify(x => x.Info(It.Is<String>(m => m.Contains("comic 404"))), Times.Once);
            this._mockLog.Verify(x => x.Warning(It.IsAny<String>()), Times.Never);
        }

        [Fact]
        public async Task GetComicAsync_Comic404AnsweredWithJson_IsParsedLikeAnyOtherComic()
        {
            var handler = StubHttpMessageHandler.Json("""{"num": 404, "title": "Found It", "img": "https://imgs.xkcd.com/comics/found.png"}""");

            var comic = await this.CreateClient(handler).GetComicAsync(404);

            Assert.Equal(new xkcdComic(404, "Found It", new Uri("https://imgs.xkcd.com/comics/found.png")), comic);
        }

        [Fact]
        public async Task GetComicAsync_Comic404OtherFailure_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.InternalServerError, new StringContent("oops"));

            var comic = await this.CreateClient(handler).GetComicAsync(404);

            Assert.Null(comic);
            this.VerifyWarning("comic 404", "500");
        }

        [Fact]
        public async Task GetLatestAsync_NotFound_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.NotFound, new StringContent("<html>404 Not Found</html>"));

            var comic = await this.CreateClient(handler).GetLatestAsync();

            Assert.Null(comic);
            this.VerifyWarning("the latest comic", "404");
        }

        [Fact]
        public async Task GetImageAsync_NotFound_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.NotFound, new StringContent("<html>404 Not Found</html>"));

            var result = await this.CreateClient(handler).GetImageAsync(new Uri("https://imgs.xkcd.com/comics/python.png"));

            Assert.Null(result);
            this.VerifyWarning("python.png", "404");
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, "500")]
        [InlineData(HttpStatusCode.TooManyRequests, "429")]
        [InlineData(HttpStatusCode.Found, "302")]
        public async Task GetComicAsync_UnsuccessfulStatus_ReturnsNullAndWarns(HttpStatusCode status, String code)
        {
            var handler = Respond(status, new StringContent(Fixture.Read("353.json")));

            var comic = await this.CreateClient(handler).GetComicAsync(353);

            Assert.Null(comic);
            this.VerifyWarning("comic 353", code);
        }

        [Fact]
        public async Task GetComicAsync_MalformedJson_ReturnsNullAndWarns()
        {
            var handler = StubHttpMessageHandler.Json("""{"num": "three hundred"}""");

            var comic = await this.CreateClient(handler).GetComicAsync(353);

            Assert.Null(comic);
            this.VerifyWarning("comic 353", "malformed");
        }

        [Fact]
        public async Task GetComicAsync_ResponseForAnotherComic_ReturnsNullAndWarns()
        {
            var handler = StubHttpMessageHandler.Json(Fixture.Read("1.json"));

            var comic = await this.CreateClient(handler).GetComicAsync(353);

            Assert.Null(comic);
            this.VerifyWarning("comic 353", "comic 1");
        }

        [Fact]
        public async Task GetComicAsync_NetworkError_ReturnsNullAndLogsTheComicAndTheError()
        {
            var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("No such host is known"));

            var comic = await this.CreateClient(handler).GetComicAsync(353);

            Assert.Null(comic);
            this._mockLog.Verify(x => x.Error(It.Is<String>(m => m.Contains("comic 353") && m.Contains("No such host is known"))), Times.Once);
        }

        [Fact]
        public async Task GetLatestAsync_Timeout_ReturnsNullAndWarns()
        {
            // HttpClient reports its own timeout as a cancellation the caller didn't ask for.
            var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

            var comic = await this.CreateClient(handler).GetLatestAsync();

            Assert.Null(comic);
            this.VerifyWarning("the latest comic", "timed out");
        }

        [Fact]
        public async Task GetLatestAsync_CancelledByTheCaller_ReturnsNullQuietly()
        {
            var handler = StubHttpMessageHandler.Json(Fixture.Read("latest-3309.json"));
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                var comic = await this.CreateClient(handler).GetLatestAsync(cancellation.Token);

                Assert.Null(comic);
            }

            this._mockLog.Verify(x => x.Warning(It.IsAny<String>()), Times.Never);
            this._mockLog.Verify(x => x.Error(It.IsAny<String>()), Times.Never);
        }

        [Fact]
        public async Task GetLatestAsync_JsonLargerThanTheCap_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.OK, new ByteArrayContent(new Byte[xkcdClient.MaxJsonBytes + 1]));

            var comic = await this.CreateClient(handler).GetLatestAsync();

            Assert.Null(comic);
            this.VerifyWarning("the latest comic", "larger than");
        }

        [Fact]
        public async Task GetImageAsync_AllowedUrl_ReturnsTheBytes()
        {
            var bytes = new Byte[] { 0x89, 0x50, 0x4E, 0x47 };
            var handler = Respond(HttpStatusCode.OK, ImageContent(bytes));
            var url = new Uri("https://imgs.xkcd.com/comics/python.png");

            var result = await this.CreateClient(handler).GetImageAsync(url);

            Assert.Equal(url, Assert.Single(handler.Requests).RequestUri);
            Assert.Equal(bytes, result);
        }

        [Theory]
        [InlineData("https://evil.example/comics/python.png")]
        [InlineData("https://imgs.xkcd.com/comics/")]
        public async Task GetImageAsync_DisallowedUrl_ReturnsNullWithoutARequest(String url)
        {
            var handler = Respond(HttpStatusCode.OK, ImageContent(new Byte[4]));

            var result = await this.CreateClient(handler).GetImageAsync(new Uri(url));

            Assert.Null(result);
            Assert.Empty(handler.Requests);
            this.VerifyWarning(url);
        }

        [Fact]
        public async Task GetImageAsync_NullUrl_ReturnsNullWithoutARequest()
        {
            var handler = Respond(HttpStatusCode.OK, ImageContent(new Byte[4]));

            var result = await this.CreateClient(handler).GetImageAsync(null);

            Assert.Null(result);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task GetImageAsync_NotAnImage_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.OK, new StringContent("<html>not an image</html>", null, "text/html"));

            var result = await this.CreateClient(handler).GetImageAsync(new Uri("https://imgs.xkcd.com/comics/python.png"));

            Assert.Null(result);
            this.VerifyWarning("python.png", "text/html");
        }

        [Fact]
        public async Task GetImageAsync_ExactlyTheCap_IsAccepted()
        {
            var handler = Respond(HttpStatusCode.OK, ImageContent(new Byte[xkcdClient.MaxImageBytes]));

            var result = await this.CreateClient(handler).GetImageAsync(new Uri("https://imgs.xkcd.com/comics/python.png"));

            Assert.Equal(xkcdClient.MaxImageBytes, result.Length);
        }

        [Fact]
        public async Task GetImageAsync_DeclaredLargerThanTheCap_ReturnsNullAndWarns()
        {
            var handler = Respond(HttpStatusCode.OK, ImageContent(new Byte[xkcdClient.MaxImageBytes + 1]));

            var result = await this.CreateClient(handler).GetImageAsync(new Uri("https://imgs.xkcd.com/comics/python.png"));

            Assert.Null(result);
            this.VerifyWarning("python.png", "larger than");
        }

        [Fact]
        public async Task GetImageAsync_UndeclaredLengthLargerThanTheCap_StopsReadingAndReturnsNull()
        {
            var content = new UnknownLengthContent(new Byte[xkcdClient.MaxImageBytes + 1]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            Assert.Null(content.Headers.ContentLength);
            var handler = Respond(HttpStatusCode.OK, content);

            var result = await this.CreateClient(handler).GetImageAsync(new Uri("https://imgs.xkcd.com/comics/python.png"));

            Assert.Null(result);
            this.VerifyWarning("python.png", "larger than");
        }

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new xkcdClient(null, this._mockLog.Object));
            Assert.Throws<ArgumentNullException>(() => new xkcdClient(new HttpClient(), null));
            Assert.Throws<ArgumentNullException>(() => new xkcdClient(null));
        }

        [Fact]
        public void Constructor_Production_BuildsTheSharedHttpClient()
        {
            Assert.NotNull(new xkcdClient(this._mockLog.Object));
        }

        private static StubHttpMessageHandler Respond(HttpStatusCode status, HttpContent content) =>
            new StubHttpMessageHandler(_ => new HttpResponseMessage(status) { Content = content });

        private static ByteArrayContent ImageContent(Byte[] bytes)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return content;
        }

        private xkcdClient CreateClient(StubHttpMessageHandler handler) => new xkcdClient(new HttpClient(handler), this._mockLog.Object);

        private void VerifyWarning(params String[] fragments) =>
            this._mockLog.Verify(x => x.Warning(It.Is<String>(m => Array.TrueForAll(fragments, fragment => m.Contains(fragment)))), Times.Once);

        // Content whose length isn't known up front, like a chunked response, so only the read loop can enforce the cap.
        private sealed class UnknownLengthContent : HttpContent
        {
            private readonly Byte[] _bytes;

            public UnknownLengthContent(Byte[] bytes)
            {
                this._bytes = bytes;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => stream.WriteAsync(this._bytes, 0, this._bytes.Length);

            // Without this, HttpContent buffers the whole body before handing out a stream, which gives it a length.
            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(this._bytes, false));

            protected override Boolean TryComputeLength(out Int64 length)
            {
                length = 0;
                return false;
            }
        }
    }
}
