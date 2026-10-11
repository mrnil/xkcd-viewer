namespace Loupedeck.xkcdviewerPlugin
{
    using System;

    public enum KeyContentKind
    {
        // Padding: drawn as a plain black key (a null image would show "action not available").
        Blank,

        PreviousArrow,
        NextArrow,

        // The Random folder's Next key: another random comic. Its Prev key is Blank.
        ShowAnother,

        // One sixth of the comic picture, or of the title card if the picture turns out not to fit (ComicFit decides
        // that once the image is decoded).
        ComicTile,

        // One tile of the title card, for a comic with no image. Text is that tile's share (TitleCardLayout).
        TitleCardTile,

        // Loading or offline. Text is shown on this tile only and may be empty.
        LoadingTile,
        OfflineTile,
    }

    // What one key of the folder shows, decided from the service's ComicView. Drawing it is the folder's job.
    // Green is set on the Comic Browser's Next chevron while a new comic is unread (and in the green half of a flash).
    public sealed record KeyContent(KeyContentKind Kind, Boolean Enabled = true, Int32 TileIndex = 0, String Text = "", Boolean Green = false)
    {
        // The status text goes on the middle tile of the top row, and the comic number below it, so neither
        // crosses a seam.
        public const Int32 StatusTile = 1;
        public const Int32 NumberTile = 4;

        // hasUnread and newComicLook come from the NewComicTracker. Only the Comic Browser shows them: its Next chevron
        // leads to the first unread comic, so it's enabled even on the latest comic known before, and green in the
        // tracker's green phases.
        public static KeyContent For(
            FolderSlot slot,
            ComicView view,
            ComicFolderMode mode = ComicFolderMode.Browse,
            Boolean hasUnread = false,
            NewComicLook newComicLook = NewComicLook.Normal)
        {
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(view);

            return slot.Kind switch
            {
                FolderSlotKind.Previous when mode == ComicFolderMode.Random => new KeyContent(KeyContentKind.Blank),
                FolderSlotKind.Next when mode == ComicFolderMode.Random => new KeyContent(KeyContentKind.ShowAnother),
                FolderSlotKind.Next when mode == ComicFolderMode.Browse && hasUnread =>
                    new KeyContent(KeyContentKind.NextArrow, Enabled: true, Green: newComicLook == NewComicLook.Green),
                FolderSlotKind.Previous => new KeyContent(KeyContentKind.PreviousArrow, view.CanGoPrevious),
                FolderSlotKind.Next => new KeyContent(KeyContentKind.NextArrow, view.CanGoNext),
                FolderSlotKind.ComicTile => ForTile(slot.Index, view),
                _ => new KeyContent(KeyContentKind.Blank),
            };
        }

        private static KeyContent ForTile(Int32 tile, ComicView view) => view.State switch
        {
            ComicViewState.Ready when view.Image != null => new KeyContent(KeyContentKind.ComicTile, TileIndex: tile),
            ComicViewState.Ready => new KeyContent(KeyContentKind.TitleCardTile, TileIndex: tile, Text: TitleCardLayout.Layout(view.Comic.Num, view.Comic.Title)[tile]),
            ComicViewState.Loading => new KeyContent(KeyContentKind.LoadingTile, TileIndex: tile, Text: StatusText(tile, "loading…", view.Num)),
            _ => new KeyContent(KeyContentKind.OfflineTile, TileIndex: tile, Text: StatusText(tile, "offline", view.Num)),
        };

        private static String StatusText(Int32 tile, String status, Int32? num) => tile switch
        {
            StatusTile => status,
            NumberTile when num.HasValue => "#" + ComicNumbers.FormatSaved(num.Value),
            _ => String.Empty,
        };
    }
}
