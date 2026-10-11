namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Xml.Linq;

    using Xunit;

    public class ChevronTests
    {
        private static readonly XNamespace _svg = "http://www.w3.org/2000/svg";

        [Theory]
        [InlineData(true, "40,26 62,50 40,74")]
        [InlineData(false, "60,26 38,50 60,74")]
        public void Svg_PointsTheTipTheRightWay(Boolean pointsRight, String points)
        {
            var line = XDocument.Parse(Chevron.Svg(pointsRight, Chevron.Enabled)).Root.Element(_svg + "polyline");

            Assert.Equal(points, line.Attribute("points").Value);
        }

        [Fact]
        public void Svg_IsOneRoundedStroke_SoTheTipHasNoNotch()
        {
            var line = XDocument.Parse(Chevron.Svg(true, Chevron.Enabled)).Root.Element(_svg + "polyline");

            Assert.Equal("round", line.Attribute("stroke-linecap").Value);
            Assert.Equal("round", line.Attribute("stroke-linejoin").Value);
            Assert.Equal("none", line.Attribute("fill").Value);
        }

        [Fact]
        public void Svg_UsesTheGivenColour()
        {
            var line = XDocument.Parse(Chevron.Svg(true, "#15B01A")).Root.Element(_svg + "polyline");

            Assert.Equal("#15B01A", line.Attribute("stroke").Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Svg_RejectsAMissingColour(String stroke)
        {
            Assert.Throws<ArgumentException>(() => Chevron.Svg(true, stroke));
        }
    }
}
