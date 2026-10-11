namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Globalization;
    using System.Security;

    // The diagonal banner drawn across the logo on the Latest and Random folder keys ("latest", "random"). BitmapBuilder
    // can only rotate in 90-degree steps, so the banner is an SVG, rendered by KeyRenderer like the logo itself.
    //
    // It runs from bottom left to top right at 30 degrees, between the eyes and the "xkcd" lettering, across the whole
    // key. Chosen by the owner on 2026-10-11 from previews, over a smaller corner ribbon.
    internal static class LogoBanner
    {
        // xkcd.com's own header blue-grey, slightly see-through so the logo shows behind it.
        public const String DefaultFill = "#6E7B91";

        public static String Svg(String label, String fill = DefaultFill)
        {
            if (String.IsNullOrEmpty(label))
            {
                throw new ArgumentException("A banner needs a label", nameof(label));
            }

            // Drawn on a 100 x 100 canvas, rotated about its centre; the band is wider than the key so its ends never show.
            const Double BandTop = 41;
            const Double BandHeight = 17;
            var baseline = (BandTop + BandHeight * 0.74).ToString(CultureInfo.InvariantCulture);

            return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">"
                + "<g transform=\"rotate(-30 50 50)\">"
                + $"<rect x=\"-40\" y=\"{BandTop}\" width=\"180\" height=\"{BandHeight}\" fill=\"{SecurityElement.Escape(fill)}\" fill-opacity=\"0.92\"/>"
                + $"<text x=\"50\" y=\"{baseline}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"13\" font-weight=\"bold\" fill=\"#FFFFFF\" text-anchor=\"middle\">{SecurityElement.Escape(label)}</text>"
                + "</g></svg>";
        }
    }
}
