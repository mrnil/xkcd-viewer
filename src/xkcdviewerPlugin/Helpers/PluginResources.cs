namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Reflection;

    // Reads files embedded in the plugin assembly (Build Action "Embedded Resource"). Trimmed to what the plugin uses;
    // the SDK's assembly extensions this wraps also offer GetStream, ReadBinaryFile, ReadImage and FindFiles, so add a
    // one-line wrapper here if one is ever needed.
    internal static class PluginResources
    {
        private static Assembly _assembly;

        public static void Init(Assembly assembly)
        {
            assembly.CheckNullArgument(nameof(assembly));
            PluginResources._assembly = assembly;
        }

        // Finds the first resource file with this file name (no namespace prefix) and returns its full name.
        // Throws `FileNotFoundException` if there's none.
        public static String FindFile(String fileName) => PluginResources._assembly.FindFileOrThrow(fileName);

        // Reads a text resource, found by file name as FindFile does.
        // Throws `FileNotFoundException` if there's none.
        public static String ReadTextFile(String resourceName) => PluginResources._assembly.ReadTextFile(PluginResources.FindFile(resourceName));
    }
}
