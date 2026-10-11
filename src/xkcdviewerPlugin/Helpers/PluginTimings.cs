namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    public static class PluginTimings
    {
        // Covers the whole request, body included. xkcd's JSON and images are small, so this only trips when
        // the network is effectively down, and a key isn't left waiting on "loading" for long.
        public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

        // After a comic (or the latest comic) fails to load, don't ask xkcd for it again until this has passed,
        // so pressing Prev / Next back and forth while offline doesn't send a request each time.
        public static readonly TimeSpan FailedLoadRetryDelay = TimeSpan.FromSeconds(30);

        // How long Prev / Next must be held to jump to the first / newest comic instead of stepping (owner's choice).
        public static readonly TimeSpan HoldToJump = TimeSpan.FromSeconds(2);

        // When a new comic is found, the New Comic key and the Comic Browser's Next chevron flash between green and
        // their usual look for this long, switching every NewComicFlashInterval, then stay green (owner's choice).
        public static readonly TimeSpan NewComicFlash = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan NewComicFlashInterval = TimeSpan.FromMilliseconds(500);

        // The new-comic check waits for its next slot in steps no longer than this, rechecking the clock each time, so
        // a computer waking from sleep isn't left waiting on a timer that stopped counting.
        public static readonly TimeSpan NewComicCheckWaitStep = TimeSpan.FromMinutes(15);
    }
}
