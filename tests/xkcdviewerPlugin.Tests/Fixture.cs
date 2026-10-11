namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.IO;

    internal static class Fixture
    {
        public static String Read(String fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }
}
