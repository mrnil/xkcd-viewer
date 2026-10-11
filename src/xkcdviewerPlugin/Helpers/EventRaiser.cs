namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    // Raises an event one handler at a time, so a handler that throws is logged and can't stop the handlers after it
    // or reach the code that raised the event. Every event in the plugin is raised through here: the subscribers are
    // independent keys, and one failing to redraw mustn't leave the others stale.
    internal static class EventRaiser
    {
        public static void Raise(EventHandler handlers, Object sender, IPluginLog log, String eventName)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(sender, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    EventRaiser.LogFailure(log, eventName, ex);
                }
            }
        }

        public static void Raise<T>(EventHandler<T> handlers, Object sender, T args, IPluginLog log, String eventName)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (EventHandler<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(sender, args);
                }
                catch (Exception ex)
                {
                    EventRaiser.LogFailure(log, eventName, ex);
                }
            }
        }

        private static void LogFailure(IPluginLog log, String eventName, Exception ex) =>
            log?.Error($"A {eventName} handler failed: {ex.Message}");
    }
}
