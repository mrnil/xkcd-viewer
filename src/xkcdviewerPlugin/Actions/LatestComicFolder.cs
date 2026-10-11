namespace Loupedeck.xkcdviewerPlugin
{
    // The Latest Comic folder: the Comic Browser opened at the latest comic, with Prev / Next from there.
    //
    // Users place this class by name, so renaming it or its namespace orphans their buttons.
    public class LatestComicFolder : ComicFolder
    {
        public LatestComicFolder()
            : base("Latest Comic", ComicFolderMode.Latest, "latest")
        {
        }
    }
}
