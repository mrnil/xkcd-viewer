namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Globalization;

    // Prev / Next stepping through comic numbers. Both return the current number unchanged when there's
    // nowhere to go, so a caller can tell "no move" by comparing.
    public static class ComicNumbers
    {
        public const Int32 First = 1;

        // Comic 404 is a joke: its only content is xkcd answering HTTP 404. It's a comic like any other for
        // Prev / Next; xkcdClient is the one place that treats it specially.
        public const Int32 NotFound = 404;

        // The last-viewed comic is saved as a plugin setting. A value that isn't a plain comic number (a corrupt or
        // hand-edited setting) is ignored, and the folder starts at the latest comic instead.
        public static String FormatSaved(Int32 num) => num.ToString(CultureInfo.InvariantCulture);

        public static Int32? ParseSaved(String saved) =>
            Int32.TryParse(saved, NumberStyles.None, CultureInfo.InvariantCulture, out var num) && num >= First ? num : null;

        public static Int32 Previous(Int32 current) => current <= First ? First : current - 1;

        // Checked before adding, so a latest number of Int32.MaxValue from a bad response can't overflow.
        public static Int32 Next(Int32 current, Int32 latest) => current >= latest ? current : current + 1;

        // A random comic from First to latest, never the one to avoid (the comic already showing), so "show another"
        // always shows another. randomBetween is Random.Next's contract: min inclusive, max exclusive. Picking from
        // one number fewer and stepping over the avoided one keeps every other comic equally likely. Both ranges are
        // written so a latest of Int32.MaxValue can't overflow.
        public static Int32 PickRandom(Func<Int32, Int32, Int32> randomBetween, Int32 latest, Int32? avoid)
        {
            ArgumentNullException.ThrowIfNull(randomBetween);

            if (latest <= First)
            {
                return First;
            }

            if (avoid is Int32 skip && skip >= First && skip <= latest)
            {
                var pick = randomBetween(First, latest);
                return pick >= skip ? pick + 1 : pick;
            }

            return randomBetween(First - 1, latest) + 1;
        }
    }
}
