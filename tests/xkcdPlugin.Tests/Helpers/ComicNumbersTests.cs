namespace Loupedeck.xkcdPlugin.Tests
{
    using System;

    using Xunit;

    public class ComicNumbersTests
    {
        private const Int32 Latest = 3000;

        [Theory]
        [InlineData(353, 352)]
        [InlineData(2, 1)]
        [InlineData(405, 403)]
        [InlineData(404, 403)]
        [InlineData(Latest, Latest - 1)]
        public void Previous_PastTheFirstComic_StepsBack(Int32 current, Int32 expected)
        {
            Assert.Equal(expected, ComicNumbers.Previous(current));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(-5)]
        public void Previous_AtOrBeforeTheFirstComic_StaysOnTheFirst(Int32 current)
        {
            Assert.Equal(ComicNumbers.First, ComicNumbers.Previous(current));
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(352, 353)]
        [InlineData(403, 405)]
        [InlineData(404, 405)]
        [InlineData(Latest - 1, Latest)]
        public void Next_BeforeTheLatestComic_StepsForward(Int32 current, Int32 expected)
        {
            Assert.Equal(expected, ComicNumbers.Next(current, Latest));
        }

        [Theory]
        [InlineData(Latest)]
        [InlineData(Latest + 1)]
        public void Next_AtOrPastTheLatestComic_DoesNotMove(Int32 current)
        {
            Assert.Equal(current, ComicNumbers.Next(current, Latest));
        }

        [Fact]
        public void Next_OnlyThe404GapIsLeft_DoesNotMove()
        {
            Assert.Equal(403, ComicNumbers.Next(403, 404));
        }
    }
}
