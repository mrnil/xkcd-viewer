namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Xunit;

    public class ComicJsonTests
    {
        [Theory]
        [InlineData("latest-3309.json", 3309, "Dogcatcher", "https://imgs.xkcd.com/comics/dogcatcher.png")]
        [InlineData("1.json", 1, "Barrel - Part 1", "https://imgs.xkcd.com/comics/barrel_cropped_(1).jpg")]
        [InlineData("353.json", 353, "Python", "https://imgs.xkcd.com/comics/python.png")]
        public void TryParse_RealResponse_MapsTheFields(String fixture, Int32 num, String title, String imageUrl)
        {
            Assert.True(ComicJson.TryParse(Fixture.Read(fixture), out var comic, out var problem));

            Assert.Null(problem);
            Assert.Equal(new xkcdComic(num, title, new Uri(imageUrl)), comic);
        }

        [Fact]
        public void TryParse_TitleWithAnHtmlEntity_IsDecoded()
        {
            ComicJson.TryParse(Fixture.Read("259.json"), out var comic, out _);

            Assert.Equal("Clichéd Exchanges", comic.Title);
        }

        [Fact]
        public void TryParse_TitleWithHtmlTags_HasThemStripped()
        {
            ComicJson.TryParse(Fixture.Read("472.json"), out var comic, out _);

            Assert.Equal("House of Pancakes", comic.Title);
        }

        [Theory]
        [InlineData("1608.json", 1608, "Hoverboard")]
        [InlineData("1663.json", 1663, "Garden")]
        public void TryParse_InteractiveComic_HasNoImage(String fixture, Int32 num, String title)
        {
            Assert.True(ComicJson.TryParse(Fixture.Read(fixture), out var comic, out _));

            Assert.Equal(new xkcdComic(num, title, null), comic);
            Assert.Equal(new Uri($"https://xkcd.com/{num}/"), comic.PageUrl);
        }

        [Theory]
        [InlineData("""{"num": 7, "img": "https://evil.example/x.png"}""")]
        [InlineData("""{"num": 7, "img": "not a url"}""")]
        public void TryParse_UnacceptableImage_IsDroppedButTheComicIsKept(String json)
        {
            Assert.True(ComicJson.TryParse(json, out var comic, out _));
            Assert.Null(comic.ImageUrl);
        }

        [Theory]
        [InlineData("""{"num": 7, "title": "", "safe_title": "Safe"}""", "Safe")]
        [InlineData("""{"num": 7, "title": "<b></b>", "safe_title": "Safe"}""", "Safe")]
        [InlineData("""{"num": 7}""", "#7")]
        [InlineData("""{"num": 7, "title": " \n\t ", "safe_title": null}""", "#7")]
        public void TryParse_NoUsableTitle_FallsBack(String json, String expected)
        {
            Assert.True(ComicJson.TryParse(json, out var comic, out _));

            Assert.Equal(expected, comic.Title);
        }

        [Theory]
        [InlineData("""{"title": "No number"}""", "num")]
        [InlineData("""{"num": 0}""", "num")]
        [InlineData("""{"num": -3}""", "num")]
        [InlineData("""{"num": null}""", "num")]
        [InlineData("""{"num": "353"}""", "malformed")]
        [InlineData("""{"num": 99999999999}""", "malformed")]
        [InlineData("""{"num": 1.5}""", "malformed")]
        [InlineData("""{"num": 7, "img": 12}""", "malformed")]
        [InlineData("""<html><head><title>404 Not Found</title></head></html>""", "malformed")]
        [InlineData("""[1, 2, 3]""", "malformed")]
        [InlineData("""null""", "num")]
        [InlineData("", "malformed")]
        [InlineData(null, "malformed")]
        public void TryParse_UnusableResponse_FailsAndSaysWhy(String json, String expectedProblem)
        {
            Assert.False(ComicJson.TryParse(json, out var comic, out var problem));

            Assert.Null(comic);
            Assert.Contains(expectedProblem, problem, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Python", "Python")]
        [InlineData("  Two   words \r\n here\t", "Two words here")]
        [InlineData("Tab\u0007Bell", "Tab Bell")]
        [InlineData("a &lt;b&gt; tag", "a <b> tag")]
        [InlineData("<i>Ital</i>ic &amp; bold", "Italic & bold")]
        [InlineData("Ünïcödé ✓ 🙂", "Ünïcödé ✓ 🙂")]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void NormalizeTitle_RawTitle_IsDisplayReady(String raw, String expected)
        {
            Assert.Equal(expected, ComicJson.NormalizeTitle(raw));
        }

        [Fact]
        public void NormalizeTitle_VeryLong_IsTruncatedWithAnEllipsis()
        {
            var result = ComicJson.NormalizeTitle(new String('x', 10_000));

            Assert.Equal(ComicJson.MaxTitleLength, result.Length);
            Assert.EndsWith("…", result, StringComparison.Ordinal);
        }

        [Fact]
        public void NormalizeTitle_TruncatedAtAnEmoji_DoesNotSplitIt()
        {
            // The emoji's two UTF-16 halves straddle the cut point.
            var raw = new String('x', ComicJson.MaxTitleLength - 2) + "🙂" + new String('y', 50);

            var result = ComicJson.NormalizeTitle(raw);

            Assert.Equal(new String('x', ComicJson.MaxTitleLength - 2) + "…", result);
        }
    }
}
