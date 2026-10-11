namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Security;

    // The Prev / Next chevron, as SVG, rendered by KeyRenderer like the logo. One stroke with rounded ends and a rounded
    // join: the earlier two separate BitmapBuilder lines looked blocky and left a notch at the tip (owner, 2026-10-11).
    internal static class Chevron
    {
        public const String Enabled = "#FFFFFF";
        public const String Disabled = "#464646";

        public static String Svg(Boolean pointsRight, String stroke)
        {
            if (String.IsNullOrEmpty(stroke))
            {
                throw new ArgumentException("A chevron needs a colour", nameof(stroke));
            }

            // On a 100 x 100 canvas, centred: the tip at 62 (or 38) and the arms at 40 (or 60), from 26 to 74 down.
            var points = pointsRight ? "40,26 62,50 40,74" : "60,26 38,50 60,74";
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">"
                + $"<polyline points=\"{points}\" fill=\"none\" stroke=\"{SecurityElement.Escape(stroke)}\" stroke-width=\"9\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"
                + "</svg>";
        }
    }
}
