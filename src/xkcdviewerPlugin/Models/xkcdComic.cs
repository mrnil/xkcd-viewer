namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    // A comic as the plugin uses it, already validated: Num is at least 1, Title is display-ready text, and
    // ImageUrl is either an allowed xkcd image URL or null for comics without a single image (interactive ones).
    // Published is the date xkcd gives (its year / month / day fields), or null if they're missing or invalid; the
    // new-comic check uses it to tell whether today's comic is out.
    public sealed record xkcdComic(Int32 Num, String Title, Uri ImageUrl, DateOnly? Published = null)
    {
        public Uri PageUrl => xkcdUrls.Page(this.Num);
    }
}
