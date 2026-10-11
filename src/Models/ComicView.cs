namespace Loupedeck.xkcdPlugin
{
    using System;

    public enum ComicViewState
    {
        // Still being fetched, or not asked for yet.
        Loading,

        // Comic is set. Image is null when the comic has no image, or its image couldn't be fetched: draw the
        // title card either way.
        Ready,

        // The comic's details couldn't be fetched (offline, or xkcd failed). Retried on a later visit.
        Failed,
    }

    // What the folder draws: a snapshot of the service's state at one moment. Num is null only before the
    // first comic is known (a first run, before the latest comic has arrived).
    public sealed record ComicView(Int32? Num, ComicViewState State, xkcdComic Comic, Byte[] Image, Boolean CanGoPrevious, Boolean CanGoNext);
}
