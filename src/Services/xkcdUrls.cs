namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Globalization;
    using System.IO;

    // Every URL the plugin requests or opens is built or checked here. See docs/ai/secure-coding.md, "This Plugin".
    public static class xkcdUrls
    {
        public const String ImageHost = "imgs.xkcd.com";

        private const String ImagePathPrefix = "/comics/";

        private static readonly String[] _imageExtensions = { ".png", ".jpg", ".jpeg", ".gif" };

        public static Uri Latest { get; } = new Uri("https://xkcd.com/info.0.json");

        public static Uri Comic(Int32 num) => new Uri(String.Create(CultureInfo.InvariantCulture, $"https://xkcd.com/{num}/info.0.json"));

        public static Uri Page(Int32 num) => new Uri(String.Create(CultureInfo.InvariantCulture, $"https://xkcd.com/{num}/"));

        // The browser is only ever opened on a comic page the plugin built itself: https://xkcd.com/{num}/.
        public static Boolean IsComicPage(Uri url) =>
            url != null
            && url.IsAbsoluteUri
            && url.Scheme == Uri.UriSchemeHttps
            && url.IsDefaultPort
            && String.IsNullOrEmpty(url.UserInfo)
            && url.Host == "xkcd.com"
            && String.IsNullOrEmpty(url.Query)
            && String.IsNullOrEmpty(url.Fragment)
            && url.Segments.Length == 2
            && url.AbsolutePath.EndsWith('/')
            && ComicNumbers.ParseSaved(url.Segments[1].TrimEnd('/')).HasValue;

        // The img field is untrusted, so only an https image file under xkcd's own image host is fetched.
        // Interactive comics have "https://imgs.xkcd.com/comics/" with no file name, which fails the extension check.
        public static Boolean IsAllowedImage(Uri url) =>
            url != null
            && url.IsAbsoluteUri
            && url.Scheme == Uri.UriSchemeHttps
            && url.IsDefaultPort
            && String.IsNullOrEmpty(url.UserInfo)
            && String.Equals(url.Host, ImageHost, StringComparison.OrdinalIgnoreCase)
            && url.AbsolutePath.StartsWith(ImagePathPrefix, StringComparison.Ordinal)
            && Array.Exists(_imageExtensions, extension => String.Equals(Path.GetExtension(url.AbsolutePath), extension, StringComparison.OrdinalIgnoreCase));
    }
}
