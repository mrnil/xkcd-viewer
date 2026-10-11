namespace Loupedeck.xkcdPlugin
{
    using System;

    public enum ComicDisplay
    {
        Picture,
        TitleCard,
    }

    // Where to draw the comic within the comic area. A title card always takes the whole area.
    public sealed record ComicPlacement(ComicDisplay Display, Int32 X, Int32 Y, Int32 Width, Int32 Height);

    // Decides how a comic image fits the 3:2 comic area: scaled to fit and letterboxed, or, when even a thumbnail
    // would be meaningless, a title card instead. See docs/ai/product.md, "Comic shapes".
    public static class ComicFit
    {
        // Shrunk below this, a large comic's detail is too fine to make out on a key. The threshold is against the
        // area actually drawn, so a device with smaller keys switches to the title card sooner, which is intended.
        public const Double MinimumScale = 0.2;

        // A comic so tall or so wide that it would cover less than this share of the area is a sliver, not a
        // picture. Shape alone decides this, so it's the same on every device.
        public const Double MinimumCoverage = 0.25;

        public static ComicPlacement Place(Int32 imageWidth, Int32 imageHeight, Int32 areaWidth, Int32 areaHeight)
        {
            if (areaWidth <= 0 || areaHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(areaWidth), $"The comic area must have a positive size, not {areaWidth}x{areaHeight}");
            }

            var titleCard = new ComicPlacement(ComicDisplay.TitleCard, 0, 0, areaWidth, areaHeight);

            // The image size comes from a downloaded file, so a nonsensical one gets the title card, not an exception.
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return titleCard;
            }

            var scale = Math.Min((Double)areaWidth / imageWidth, (Double)areaHeight / imageHeight);
            var coverage = imageWidth * scale * imageHeight * scale /((Double)areaWidth * areaHeight);

            if (scale < MinimumScale || coverage < MinimumCoverage)
            {
                return titleCard;
            }

            var width = Math.Clamp((Int32)Math.Round(imageWidth * scale), 1, areaWidth);
            var height = Math.Clamp((Int32)Math.Round(imageHeight * scale), 1, areaHeight);
            return new ComicPlacement(ComicDisplay.Picture, (areaWidth - width) / 2, (areaHeight - height) / 2, width, height);
        }
    }
}
