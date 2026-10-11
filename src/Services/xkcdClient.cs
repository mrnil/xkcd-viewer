namespace Loupedeck.xkcdPlugin
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    // The only class that talks to xkcd. It requests URLs only from xkcdUrls, caps every response body before
    // reading it, and turns every failure into a logged null (see IxkcdClient).
    public sealed class xkcdClient : IxkcdClient
    {
        // The biggest real response seen is a few KB (#405 at 1.4 KB, plus interactive comics' extra_parts).
        public const Int32 MaxJsonBytes = 256 * 1024;

        // The biggest standard comic images are well under 1 MB.
        public const Int32 MaxImageBytes = 4 * 1024 * 1024;

        private static readonly HttpClient _sharedHttpClient = CreateHttpClient();

        private readonly HttpClient _httpClient;
        private readonly IPluginLog _log;

        public xkcdClient(IPluginLog log)
            : this(_sharedHttpClient, log)
        {
        }

        internal xkcdClient(HttpClient httpClient, IPluginLog log)
        {
            this._httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this._log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public Task<xkcdComic> GetLatestAsync(CancellationToken cancellationToken = default) =>
            this.GetComicJsonAsync(xkcdUrls.Latest, "the latest comic", null, cancellationToken);

        public Task<xkcdComic> GetComicAsync(Int32 num, CancellationToken cancellationToken = default)
        {
            if (num < ComicNumbers.First || num == ComicNumbers.Missing)
            {
                this._log.Warning($"Cannot get comic {num} - there is no such comic");
                return Task.FromResult<xkcdComic>(null);
            }

            return this.GetComicJsonAsync(xkcdUrls.Comic(num), $"comic {num}", num, cancellationToken);
        }

        public async Task<Byte[]> GetImageAsync(Uri imageUrl, CancellationToken cancellationToken = default)
        {
            if (!xkcdUrls.IsAllowedImage(imageUrl))
            {
                this._log.Warning($"Cannot get image {imageUrl} - only {xkcdUrls.ImageHost} comic images are fetched");
                return null;
            }

            return await this.GetBodyAsync(imageUrl, $"image {imageUrl}", MaxImageBytes, IsImage, cancellationToken);
        }

        private static Boolean IsImage(HttpContentHeaders headers) =>
            headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

        private static HttpClient CreateHttpClient()
        {
            // No redirects: a redirect could lead anywhere, and xkcd's https endpoints don't use them.
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            };

            var client = new HttpClient(handler) { Timeout = PluginTimings.HttpTimeout };
            var version = typeof(xkcdClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("xkcdPlugin", version));
            return client;
        }

        private static async Task<Byte[]> ReadCappedAsync(HttpContent content, Int32 maxBytes, CancellationToken cancellationToken)
        {
            if (content.Headers.ContentLength > maxBytes)
            {
                return null;
            }

            using (var stream = await content.ReadAsStreamAsync(cancellationToken))
            using (var buffer = new MemoryStream())
            {
                var chunk = new Byte[81920];
                Int32 read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    if (buffer.Length + read > maxBytes)
                    {
                        return null;
                    }

                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }

        private async Task<xkcdComic> GetComicJsonAsync(Uri url, String entity, Int32? expectedNum, CancellationToken cancellationToken)
        {
            var body = await this.GetBodyAsync(url, entity, MaxJsonBytes, null, cancellationToken);
            if (body == null)
            {
                return null;
            }

            if (!ComicJson.TryParse(Encoding.UTF8.GetString(body), out var comic, out var problem))
            {
                this._log.Warning($"Ignoring {entity} - {problem}");
                return null;
            }

            if (expectedNum.HasValue && comic.Num != expectedNum.Value)
            {
                this._log.Warning($"Ignoring {entity} - the response is for comic {comic.Num}");
                return null;
            }

            return comic;
        }

        private async Task<Byte[]> GetBodyAsync(
            Uri url, String entity, Int32 maxBytes, Func<HttpContentHeaders, Boolean> isExpectedContent, CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await this._httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        this._log.Warning($"Cannot get {entity} - xkcd says it doesn't exist (404)");
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        this._log.Warning($"Cannot get {entity} - xkcd answered {(Int32)response.StatusCode} {response.StatusCode}");
                        return null;
                    }

                    if (isExpectedContent != null && !isExpectedContent(response.Content.Headers))
                    {
                        this._log.Warning($"Cannot get {entity} - unexpected content type {response.Content.Headers.ContentType?.MediaType ?? "(none)"}");
                        return null;
                    }

                    var body = await ReadCappedAsync(response.Content, maxBytes, cancellationToken);
                    if (body == null)
                    {
                        this._log.Warning($"Cannot get {entity} - the response is larger than {maxBytes} bytes");
                    }

                    return body;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                this._log.Verbose($"Stopped getting {entity} - cancelled");
                return null;
            }
            catch (OperationCanceledException)
            {
                this._log.Warning($"Cannot get {entity} - timed out after {this._httpClient.Timeout.TotalSeconds:0} s");
                return null;
            }
            catch (Exception ex)
            {
                this._log.Error($"Failed to get {entity}: {ex.Message}");
                return null;
            }
        }
    }
}
