namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Collections.Generic;

    using Xunit;

    public class ComicNumbersTests
    {
        private const Int32 Latest = 3000;

        [Theory]
        [InlineData(353, 352)]
        [InlineData(2, 1)]
        [InlineData(405, 404)]
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
        [InlineData(403, 404)]
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
        public void Next_AtTheLargestPossibleNumber_DoesNotOverflow()
        {
            Assert.Equal(Int32.MaxValue, ComicNumbers.Next(Int32.MaxValue, Int32.MaxValue));
        }

        [Fact]
        public void Next_Comic404IsTheLatest_StepsOntoIt()
        {
            Assert.Equal(404, ComicNumbers.Next(403, 404));
        }

        [Theory]
        [InlineData("353", 353)]
        [InlineData("1", 1)]
        [InlineData("404", 404)]
        public void ParseSaved_ComicNumber_ReturnsIt(String saved, Int32 expected)
        {
            Assert.Equal(expected, ComicNumbers.ParseSaved(saved));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-3")]
        [InlineData("+3")]
        [InlineData(" 3")]
        [InlineData("3.0")]
        [InlineData("1,000")]
        [InlineData("99999999999")]
        [InlineData("latest")]
        public void ParseSaved_NotAComicNumber_ReturnsNull(String saved)
        {
            Assert.Null(ComicNumbers.ParseSaved(saved));
        }

        [Fact]
        public void FormatSaved_RoundTripsThroughParseSaved()
        {
            Assert.Equal(3309, ComicNumbers.ParseSaved(ComicNumbers.FormatSaved(3309)));
        }

        [Fact]
        public void PickRandom_NothingToAvoid_CoversEveryComicFromTheFirstToTheLatest()
        {
            var lowest = ComicNumbers.PickRandom((min, max) => min, Latest, null);
            var highest = ComicNumbers.PickRandom((min, max) => max - 1, Latest, null);

            Assert.Equal(ComicNumbers.First, lowest);
            Assert.Equal(Latest, highest);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void PickRandom_EveryPossibleDraw_NeverPicksTheComicToAvoidAndCoversAllOthers(Int32 avoid)
        {
            const Int32 latest = 10;
            var picks = new HashSet<Int32>();

            for (var draw = 0; draw < latest - 1; draw++)
            {
                picks.Add(ComicNumbers.PickRandom((min, max) => min + draw, latest, avoid));
            }

            Assert.DoesNotContain(avoid, picks);
            Assert.Equal(latest - 1, picks.Count);
            Assert.All(picks, pick => Assert.InRange(pick, ComicNumbers.First, latest));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(Latest + 1)]
        public void PickRandom_AvoidIsNotAComic_PicksFromEveryComic(Int32 avoid)
        {
            Assert.Equal(Latest, ComicNumbers.PickRandom((min, max) => max - 1, Latest, avoid));
        }

        [Theory]
        [InlineData(1, null)]
        [InlineData(1, 1)]
        [InlineData(0, null)]
        public void PickRandom_OnlyOneComic_IsTheFirstWithoutDrawing(Int32 latest, Int32? avoid)
        {
            Assert.Equal(ComicNumbers.First, ComicNumbers.PickRandom((_, _) => throw new InvalidOperationException("drew"), latest, avoid));
        }

        [Fact]
        public void PickRandom_AtTheLargestPossibleLatest_DoesNotOverflow()
        {
            Assert.Equal(Int32.MaxValue, ComicNumbers.PickRandom((min, max) => max - 1, Int32.MaxValue, null));
            Assert.Equal(Int32.MaxValue, ComicNumbers.PickRandom((min, max) => max - 1, Int32.MaxValue, 1));
        }

        [Fact]
        public void PickRandom_NullRandomSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ComicNumbers.PickRandom(null, Latest, null));
        }
    }
}
