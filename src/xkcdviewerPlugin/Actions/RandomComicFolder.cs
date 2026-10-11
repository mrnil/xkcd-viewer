namespace Loupedeck.xkcdviewerPlugin
{
    // The Random Comic folder: a random comic each time it opens, and both top-row keys show "show another" in place
    // of Prev / Next.
    //
    // Users place this class by name, so renaming it or its namespace orphans their buttons.
    public class RandomComicFolder : ComicFolder
    {
        public RandomComicFolder()
            : base("Random Comic", ComicFolderMode.Random, "random")
        {
        }
    }
}
