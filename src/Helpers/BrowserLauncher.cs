namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Diagnostics;

    // Opens a comic's page in the default browser. The SDK has no API for this, so it goes through the OS.
    internal static class BrowserLauncher
    {
        public static void OpenComicPage(Uri url, IPluginLog log)
        {
            if (!xkcdUrls.IsComicPage(url))
            {
                log.Warning($"Not opening {url} - only xkcd comic pages are opened");
                return;
            }

            try
            {
                // AbsoluteUri is passed as one argument, never through a shell, so it can't be read as a command.
                var startInfo = OperatingSystem.IsMacOS()
                    ? new ProcessStartInfo("open") { ArgumentList = { url.AbsoluteUri } }
                    : new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true };

                using (Process.Start(startInfo))
                {
                }

                log.Info($"Opened {url} in the browser");
            }
            catch (Exception ex)
            {
                log.Error($"Failed to open {url} in the browser: {ex.Message}");
            }
        }
    }
}
