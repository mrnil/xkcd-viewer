namespace Loupedeck.xkcdPlugin
{
    using System;

    // Services log through this rather than the static PluginLog, so tests can check what gets logged.
    public interface IPluginLog
    {
        void Verbose(String text);

        void Info(String text);

        void Warning(String text);

        void Error(String text);

        void Error(Exception ex, String text);
    }
}
