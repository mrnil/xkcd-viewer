namespace Loupedeck.xkcdPlugin
{
    using System;

    // A comic as the plugin uses it, already validated: Num is at least 1, Title is display-ready text, and
    // ImageUrl is either an allowed xkcd image URL or null for comics without a single image (interactive ones).
    public sealed record xkcdComic(Int32 Num, String Title, Uri ImageUrl)
    {
        public Uri PageUrl => xkcdUrls.Page(this.Num);
    }
}
