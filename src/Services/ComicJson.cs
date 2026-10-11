namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // Turns an info.0.json response into an xkcdComic, treating every field as untrusted.
    internal static class ComicJson
    {
        public const Int32 MaxTitleLength = 100;

        private static readonly Regex _htmlTag = new Regex("<[^>]*>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public static Boolean TryParse(String json, out xkcdComic comic, out String problem)
        {
            comic = null;

            WireComic wire;
            try
            {
                wire = JsonSerializer.Deserialize<WireComic>(json ?? String.Empty);
            }
            catch (JsonException ex)
            {
                problem = $"malformed JSON: {ex.Message}";
                return false;
            }

            if (wire?.Num is not >= 1)
            {
                problem = "missing or invalid num";
                return false;
            }

            var num = wire.Num.Value;
            var title = NormalizeTitle(wire.Title);
            if (title.Length == 0)
            {
                title = NormalizeTitle(wire.SafeTitle);
            }

            if (title.Length == 0)
            {
                title = "#" + num.ToString(CultureInfo.InvariantCulture);
            }

            var imageUrl = Uri.TryCreate(wire.Img, UriKind.Absolute, out var parsed) && xkcdUrls.IsAllowedImage(parsed) ? parsed : null;

            comic = new xkcdComic(num, title, imageUrl);
            problem = null;
            return true;
        }

        // Titles can hold HTML (#472 wraps a word in a <span>) and entities (#259 has "Clich&eacute;d"). Tags are
        // stripped before decoding, so an encoded "&lt;b&gt;" shows as text rather than being stripped.
        internal static String NormalizeTitle(String raw)
        {
            if (String.IsNullOrEmpty(raw))
            {
                return String.Empty;
            }

            var decoded = WebUtility.HtmlDecode(_htmlTag.Replace(raw, String.Empty));

            var builder = new StringBuilder(decoded.Length);
            var pendingSpace = false;
            foreach (var c in decoded)
            {
                if (Char.IsWhiteSpace(c) || Char.IsControl(c))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(c);
            }

            return Truncate(builder.ToString());
        }

        private static String Truncate(String text)
        {
            if (text.Length <= MaxTitleLength)
            {
                return text;
            }

            // Leave room for the ellipsis, and don't cut a surrogate pair in half.
            var length = MaxTitleLength - 1;
            if (Char.IsHighSurrogate(text[length - 1]))
            {
                length--;
            }

            return text[..length].TrimEnd() + "…";
        }

        private sealed class WireComic
        {
            [JsonPropertyName("num")]
            public Int32? Num { get; set; }

            [JsonPropertyName("title")]
            public String Title { get; set; }

            [JsonPropertyName("safe_title")]
            public String SafeTitle { get; set; }

            [JsonPropertyName("img")]
            public String Img { get; set; }
        }
    }
}
