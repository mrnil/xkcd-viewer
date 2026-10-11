namespace Loupedeck.xkcdPlugin
{
    using System;

    public static class PluginTimings
    {
        // Covers the whole request, body included. xkcd's JSON and images are small, so this only trips when
        // the network is effectively down, and a key isn't left waiting on "loading" for long.
        public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);
    }
}
