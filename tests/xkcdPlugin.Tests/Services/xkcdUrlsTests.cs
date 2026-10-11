namespace Loupedeck.xkcdPlugin.Tests
{
    using System;

    using Xunit;

    public class xkcdUrlsTests
    {
        [Fact]
        public void Latest_IsTheLatestComicJson()
        {
            Assert.Equal("https://xkcd.com/info.0.json", xkcdUrls.Latest.ToString());
        }

        [Fact]
        public void Comic_Number_IsThatComicsJson()
        {
            Assert.Equal("https://xkcd.com/353/info.0.json", xkcdUrls.Comic(353).ToString());
        }

        [Fact]
        public void Page_Number_IsThatComicsPage()
        {
            Assert.Equal("https://xkcd.com/1234/", xkcdUrls.Page(1234).ToString());
        }

        [Theory]
        [InlineData("https://imgs.xkcd.com/comics/python.png")]
        [InlineData("https://imgs.xkcd.com/comics/barrel_cropped_(1).jpg")]
        [InlineData("https://IMGS.XKCD.COM/comics/python.PNG")]
        [InlineData("https://imgs.xkcd.com/comics/old.jpeg")]
        [InlineData("https://imgs.xkcd.com/comics/animated.gif")]
        public void IsAllowedImage_xkcdComicImage_IsAllowed(String url)
        {
            Assert.True(xkcdUrls.IsAllowedImage(new Uri(url)));
        }

        [Theory]
        [InlineData("http://imgs.xkcd.com/comics/python.png")]               // not https
        [InlineData("https://evil.example/comics/python.png")]                // another host
        [InlineData("https://imgs.xkcd.com.evil.example/comics/python.png")]  // a host that only starts the same
        [InlineData("https://user@imgs.xkcd.com/comics/python.png")]          // user info
        [InlineData("https://imgs.xkcd.com:8443/comics/python.png")]          // another port
        [InlineData("https://imgs.xkcd.com/comics/")]                         // interactive comics: no file
        [InlineData("https://imgs.xkcd.com/comics/../secret.png")]            // escapes /comics/
        [InlineData("https://imgs.xkcd.com/other/python.png")]                // outside /comics/
        [InlineData("https://imgs.xkcd.com/comics/python.svg")]               // not an image type we decode
        [InlineData("https://imgs.xkcd.com/comics/python")]                   // no extension
        [InlineData("file:///C:/comics/python.png")]
        public void IsAllowedImage_AnythingElse_IsRejected(String url)
        {
            Assert.False(xkcdUrls.IsAllowedImage(new Uri(url)));
        }

        [Fact]
        public void IsAllowedImage_RelativeOrNull_IsRejected()
        {
            Assert.False(xkcdUrls.IsAllowedImage(new Uri("/comics/python.png", UriKind.Relative)));
            Assert.False(xkcdUrls.IsAllowedImage(null));
        }
    }
}
