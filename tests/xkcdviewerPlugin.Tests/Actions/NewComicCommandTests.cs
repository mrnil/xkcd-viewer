namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using Xunit;

    // SDK glue is exempt from coverage (docs/ai/test-coverage.md); this only checks the wiring users see.
    public class NewComicCommandTests
    {
        [Fact]
        public void Constructor_SetsTheNameUsersSeeInTheFoldersGroup()
        {
            var command = new NewComicCommand();

            Assert.Equal("New Comic", command.DisplayName);
            Assert.Equal("xkcd viewer", command.GroupName);
        }

        // Without IsWidget the SDK draws the display name over the key's image (sdk-quick-reference.md).
        [Fact]
        public void Constructor_DrawsTheImageOnly()
        {
            var command = new NewComicCommand();

            Assert.True(command.IsWidget);
        }
    }
}
