namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Linq;
    using System.Xml.Linq;

    using Xunit;

    // KeyRenderer.Logo draws the embedded art/xkcdviewerlogo.svg. Drawing it needs the SDK's native renderer, which
    // tests don't load, so this checks what a broken build would get wrong: the file is embedded, under the one name
    // PluginResources finds, and it's an SVG.
    public class LogoSvgTests
    {
        private const String ResourceName = "Loupedeck.xkcdviewerPlugin.Resources.Images.xkcdviewerlogo.svg";

        [Fact]
        public void Logo_IsEmbeddedOnce_UnderTheNameKeyRendererLooksUp()
        {
            var names = typeof(xkcdviewerPlugin).Assembly.GetManifestResourceNames();

            // PluginResources finds a file by the end of its name, so a second match would make the lookup ambiguous.
            Assert.Equal(new[] { ResourceName }, names.Where(name => name.EndsWith("xkcdviewerlogo.svg", StringComparison.Ordinal)));
        }

        [Fact]
        public void Logo_IsAnSvgWithASquareViewBox()
        {
            using (var stream = typeof(xkcdviewerPlugin).Assembly.GetManifestResourceStream(ResourceName))
            {
                var svg = XDocument.Load(stream).Root;

                Assert.Equal("svg", svg.Name.LocalName);
                var viewBox = svg.Attribute("viewBox").Value.Split(' ').Select(Double.Parse).ToArray();
                Assert.Equal(4, viewBox.Length);
                Assert.Equal(viewBox[2], viewBox[3]);
            }
        }
    }
}
