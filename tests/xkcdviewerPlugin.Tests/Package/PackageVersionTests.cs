namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;

    using Xunit;

    // The manifest's version: line is the only place the version is set (the .csproj reads it), so the packed plugin
    // must carry exactly that version, three-part, everywhere a version string appears.
    public class PackageVersionTests
    {
        private static readonly Assembly _plugin = typeof(xkcdviewerPlugin).Assembly;

        private static String ManifestVersion()
        {
            var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Package", "LoupedeckPackage.yaml"));
            var match = Regex.Match(manifest, @"(?m)^version:[ \t]*([^\s#]+)");
            Assert.True(match.Success, "LoupedeckPackage.yaml has no version: line");
            return match.Groups[1].Value;
        }

        [Fact]
        public void Manifest_HasAThreePartVersion()
        {
            Assert.Matches(@"^\d+\.\d+\.\d+$", ManifestVersion());
        }

        [Fact]
        public void ProductVersion_MatchesTheManifest()
        {
            Assert.Equal(ManifestVersion(), _plugin.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        }

        [Fact]
        public void FileVersion_MatchesTheManifest()
        {
            Assert.Equal(ManifestVersion(), _plugin.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
            Assert.Equal(ManifestVersion(), FileVersionInfo.GetVersionInfo(_plugin.Location).FileVersion);
        }

        // .NET always stores four parts here, so the manifest's version plus ".0".
        [Fact]
        public void AssemblyVersion_IsTheManifestVersion()
        {
            Assert.Equal(Version.Parse(ManifestVersion() + ".0"), _plugin.GetName().Version);
        }
    }
}
