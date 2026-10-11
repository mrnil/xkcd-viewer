namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;

    using Xunit;

    // KeyRenderer.Logo draws the embedded art/xkcdviewerlogo.svg, and art/xkcdviewerlogo-closed.svg for the New Comic
    // key with nothing new. Drawing them needs the SDK's native renderer, which tests don't load, so this checks what
    // a broken build would get wrong: each file is embedded, under the one name PluginResources finds, and is an SVG
    // the same shape as the other, so the two line up.
    public class LogoSvgTests
    {
        private const String Prefix = "Loupedeck.xkcdviewerPlugin.Resources.Images.";

        [Theory]
        [InlineData("xkcdviewerlogo.svg")]
        [InlineData("xkcdviewerlogo-closed.svg")]
        public void Logo_IsEmbeddedOnce_UnderTheNameKeyRendererLooksUp(String fileName)
        {
            var names = typeof(xkcdviewerPlugin).Assembly.GetManifestResourceNames();

            // PluginResources finds a file by the end of its name, so a second match would make the lookup ambiguous.
            Assert.Equal(new[] { Prefix + fileName }, names.Where(name => name.EndsWith(fileName, StringComparison.Ordinal)));
        }

        [Fact]
        public void Logos_AreSvgsWithTheSameSquareViewBox()
        {
            var open = ViewBox("xkcdviewerlogo.svg");

            Assert.Equal(4, open.Length);
            Assert.Equal(open[2], open[3]);
            Assert.Equal(open, ViewBox("xkcdviewerlogo-closed.svg"));
        }

        private static Double[] ViewBox(String fileName)
        {
            using (var stream = typeof(xkcdviewerPlugin).Assembly.GetManifestResourceStream(Prefix + fileName))
            {
                var svg = XDocument.Load(stream).Root;
                Assert.Equal("svg", svg.Name.LocalName);
                return svg.Attribute("viewBox").Value.Split(' ').Select(value => Double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            }
        }
    }
}
