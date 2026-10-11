namespace Loupedeck.xkcdPlugin
{
    using System;

    // Prev / Next stepping through comic numbers. Both return the current number unchanged when there's
    // nowhere to go, so a caller can tell "no move" by comparing.
    public static class ComicNumbers
    {
        public const Int32 First = 1;

        // xkcd skipped this number on purpose: https://xkcd.com/404/ is a joke "not found" page with no comic.
        public const Int32 Missing = 404;

        public static Int32 Previous(Int32 current)
        {
            if (current <= First)
            {
                return First;
            }

            var previous = current - 1;
            return previous == Missing ? previous - 1 : previous;
        }

        public static Int32 Next(Int32 current, Int32 latest)
        {
            // Checked before adding, so a latest number of Int32.MaxValue from a bad response can't overflow.
            if (current >= latest)
            {
                return current;
            }

            var next = current + 1;
            if (next == Missing)
            {
                next++;
            }

            return next > latest ? current : next;
        }
    }
}
