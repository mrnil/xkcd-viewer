namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    using Xunit.Sdk;

    // Waits for something a background task will make true, instead of sleeping for a guessed time.
    internal static class TestWait
    {
        private static readonly TimeSpan _limit = TimeSpan.FromSeconds(5);

        public static async Task UntilAsync(Func<Boolean> condition, String waitingFor)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                if (stopwatch.Elapsed > _limit)
                {
                    throw new XunitException($"Gave up after {_limit.TotalSeconds:0} s waiting for {waitingFor}");
                }

                await Task.Delay(5);
            }
        }
    }
}
