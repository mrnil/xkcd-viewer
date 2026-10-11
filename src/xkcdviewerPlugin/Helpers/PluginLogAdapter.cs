namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    public sealed class PluginLogAdapter : IPluginLog
    {
        public void Verbose(String text) => PluginLog.Verbose(text);

        public void Info(String text) => PluginLog.Info(text);

        public void Warning(String text) => PluginLog.Warning(text);

        public void Error(String text) => PluginLog.Error(text);

        public void Error(Exception ex, String text) => PluginLog.Error(ex, text);
    }
}
