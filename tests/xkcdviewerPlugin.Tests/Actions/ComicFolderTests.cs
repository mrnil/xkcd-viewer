namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using Xunit;

    // SDK glue is exempt from coverage (docs/ai/test-coverage.md); this only checks the wiring users see.
    public class ComicFolderTests
    {
        [Fact]
        public void Constructor_SetsTheNameUsersSee()
        {
            var folder = new ComicFolder();

            Assert.Equal("Comic Browser", folder.DisplayName);
            Assert.True(String.IsNullOrEmpty(folder.GroupName));
        }
    }
}
