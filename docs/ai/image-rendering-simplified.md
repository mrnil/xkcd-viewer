# Image Rendering

Button images are drawn by one small static class, `KeyRenderer` (`src/xkcdviewerPlugin/Helpers/KeyRenderer.cs`), not a factory/store/data-model abstraction. Every key image in the plugin comes from it. Add a method there when an action needs a new kind of key, and only the methods actually used.

## Two Signature Families: Pick by SDK Base Class

- `PluginDynamicCommand` / `PluginDynamicFolder`: `GetCommandImage(String actionParameter, PluginImageSize imageSize)`
- `ActionEditorCommand`: `GetCommandImage(ActionEditorActionParameters actionParameters, Int32 imageWidth, Int32 imageHeight)`. But see the MX Keypad gotcha in `sdk-quick-reference.md`: its images don't reach that device

`BitmapBuilder` has constructors for both (`new BitmapBuilder(imageSize)` and `new BitmapBuilder(width, height)`). Add `(Int32, Int32)` helper overloads only when an `ActionEditorCommand` actually needs them.

## KeyRenderer

```csharp
internal static class KeyRenderer
{
    // The size of one key's image, for slicing the comic.
    public static (Int32 Width, Int32 Height) TileSize(PluginImageSize imageSize);

    // The logo (open or closed eyes) on white or the new-comic green, with an optional diagonal banner
    // ("latest", "random", "new"). The folder keys and the New Comic key.
    public static BitmapImage Logo(PluginImageSize imageSize, String label = null, Boolean green = false, Boolean eyesClosed = false);

    // A plain black key: padding, and the fallback whenever there's nothing to draw. Never return null for a folder key.
    public static BitmapImage Blank(PluginImageSize imageSize);

    // The Prev / Next chevron: dimmed when there's nowhere to go, green for the new-comic signal.
    public static BitmapImage Arrow(Boolean pointsRight, Boolean enabled, PluginImageSize imageSize, Boolean green = false);

    // Random Comic's "show another" key.
    public static BitmapImage ShowAnother(PluginImageSize imageSize);

    // "loading…" (yellow) or "offline" (grey) on a comic tile.
    public static BitmapImage StatusTile(String text, Boolean loading, PluginImageSize imageSize);

    // One tile of a title card: text already laid out by TitleCardLayout.
    public static BitmapImage TitleCardTile(String text, Int32 tileIndex, PluginImageSize imageSize);

    // Decodes and fits a comic image into the whole 3×2 area once; ComicTile then cuts one key's tile from it.
    public static BitmapImage BuildComicArea(Int32 num, Byte[] imageBytes, Int32 tileWidth, Int32 tileHeight, IPluginLog log);
    public static BitmapImage ComicTile(BitmapImage area, Int32 tileIndex, PluginImageSize imageSize);
}
```

Keep the renderer thin. Every *decision* (which text, how it wraps onto lines, which key shows what, where the comic goes) is made in a pure, SDK-free, fully tested helper: `KeyContent` decides what each folder key shows, `TitleCardLayout` wraps the title, `ComicFit` places the comic, `Chevron` and `LogoBanner` build SVG text. `KeyRenderer` only paints what it's given (see `structure.md` pattern 5). If drawing fails, log it and fall back to something drawable, never throw into the SDK: `BuildComicArea` returns null and the comic gets its title card, and `ComicFolder.GetCommandImage` catches anything else and returns `Blank`.

### Example

`NewComicCommand` draws its whole key from one decision the tracker has already made:

```csharp
protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
{
    var green = xkcdviewerPlugin.Instance?.NewComics?.Look == NewComicLook.Green;
    return green ? KeyRenderer.Logo(imageSize, "new", green: true) : KeyRenderer.Logo(imageSize, eyesClosed: true);
}
```

## Drawing Comic Images

xkcd comic images are far larger than a key (80 or 116 px square on the devices tested), and many are wide strips. The plugin downloads the image off the SDK thread, decodes it with `BitmapImage.FromArray`, fits it into the 3×2 area once per comic and tile size (`BuildComicArea`), caches that area in the folder, and cuts each key's tile from it on every draw. `product.md` → "Slicing" has the details and why tiles themselves aren't cached.

## Resource Naming

`PluginResources` resolves a resource with `PluginResources.FindFile`, a **filename-only** lookup. Don't use a namespace prefix:

```csharp
PluginResources.ReadTextFile("xkcdviewerlogo.svg");                                         // correct
PluginResources.ReadTextFile("Loupedeck.xkcdviewerPlugin.Resources.Images.xkcdviewerlogo.svg");  // wrong - and breaks if the assembly is renamed
```

The two embedded files today are the logos, embedded straight from `art/` (`xkcdviewerlogo.svg`, `xkcdviewerlogo-closed.svg`), each with its own `<EmbeddedResource>` entry in `src/xkcdviewerPlugin/xkcdviewerPlugin.csproj`, so the SVG in `art/` stays the only copy. New embedded assets that aren't shared with `art/` go in `src/xkcdviewerPlugin/Resources/`.

## Accessibility

Colour is never the only signal. Pair a status colour with a distinct icon shape or text, so colour-blind users get the same information. In this plugin, the green New Comic key also says "new", and the loading / offline tiles say "loading…" / "offline".

## When GetCommandImage Is Called

- When the button first appears
- After you request a redraw (see `icon-update-patterns.md` for which call on which base class)

The framework caches the result, so don't add your own image cache for *rendered buttons*. A cache for *downloaded source images* or for the scaled comic area is a different thing and is fine (`ImageCache`, and the folder's `LruCache` of comic areas).
