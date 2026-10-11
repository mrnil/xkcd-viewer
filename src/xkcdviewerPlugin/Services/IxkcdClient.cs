namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // Every method returns null on any failure (offline, not found, bad data, too large, cancelled) and logs why.
    // None of them throws.
    public interface IxkcdClient
    {
        Task<xkcdComic> GetLatestAsync(CancellationToken cancellationToken = default);

        Task<xkcdComic> GetComicAsync(Int32 num, CancellationToken cancellationToken = default);

        Task<Byte[]> GetImageAsync(Uri imageUrl, CancellationToken cancellationToken = default);
    }
}
