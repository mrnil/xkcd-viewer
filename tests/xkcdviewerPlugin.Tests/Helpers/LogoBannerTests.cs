namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Xml.Linq;

    using Xunit;

    public class LogoBannerTests
    {
        private static readonly XNamespace _svg = "http://www.w3.org/2000/svg";

        [Fact]
        public void Svg_PutsTheLabelOnARotatedBand()
        {
            var root = XDocument.Parse(LogoBanner.Svg("latest")).Root;

            var group = root.Element(_svg + "g");
            Assert.Equal("rotate(-30 50 50)", group.Attribute("transform").Value);
            Assert.Equal(LogoBanner.DefaultFill, group.Element(_svg + "rect").Attribute("fill").Value);
            Assert.Equal("latest", group.Element(_svg + "text").Value);
        }

        [Fact]
        public void Svg_UsesTheGivenFill()
        {
            var root = XDocument.Parse(LogoBanner.Svg("new", "#15B01A")).Root;

            Assert.Equal("#15B01A", root.Element(_svg + "g").Element(_svg + "rect").Attribute("fill").Value);
        }

        [Fact]
        public void Svg_EscapesTheLabel_SoItStaysValidSvg()
        {
            var root = XDocument.Parse(LogoBanner.Svg("<a & \"b\">")).Root;

            Assert.Equal("<a & \"b\">", root.Element(_svg + "g").Element(_svg + "text").Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Svg_RejectsAnEmptyLabel(String label)
        {
            Assert.Throws<ArgumentException>(() => LogoBanner.Svg(label));
        }
    }
}
