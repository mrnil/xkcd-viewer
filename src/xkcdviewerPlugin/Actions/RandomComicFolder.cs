namespace Loupedeck.xkcdviewerPlugin
{
    // The Random Comic folder: a random comic each time it opens, with one "show another" key where Next is (Prev's
    // key is blank).
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
