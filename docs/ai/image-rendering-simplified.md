# Image Rendering

Button images are drawn with small static helper classes, not a factory/store/data-model abstraction. Neither helper exists yet in this repo. Create them in `src/Helpers/` the first time an action needs a drawn image, using the API shape below, and add only the methods actually used.

## Two Signature Families: Pick by SDK Base Class

- `PluginDynamicCommand` / `PluginDynamicFolder`: `GetCommandImage(String actionParameter, PluginImageSize imageSize)`
- `ActionEditorCommand`: `GetCommandImage(ActionEditorActionParameters actionParameters, Int32 imageWidth, Int32 imageHeight)`. But see the MX Keypad gotcha in `sdk-quick-reference.md`: its images don't reach that device

`BitmapBuilder` has constructors for both (`new BitmapBuilder(imageSize)` and `new BitmapBuilder(width, height)`). Add `(Int32, Int32)` helper overloads only when an `ActionEditorCommand` actually needs them.

## Target API

```csharp
internal static class ButtonImageHelper
{
    // Static SVG/PNG icon from embedded resources (filename only - see Resource Naming).
    public static BitmapImage Icon(String iconResourceName);

    // Icon composited onto a solid background.
    public static BitmapImage IconWithBackground(String iconResourceName, PluginImageSize imageSize, BitmapColor backgroundColor);
}

internal static class ButtonTextRenderer
{
    // Text on a solid background; font size chosen from text length and image width.
    public static BitmapImage RenderText(String text, PluginImageSize imageSize, BitmapColor? backgroundColor = null, BitmapColor? textColor = null);

    // Icon in the upper half, text in the lower half, each centred independently.
    public static BitmapImage RenderTextWithIcon(String text, PluginImageSize imageSize, String iconResourceName, BitmapColor? backgroundColor = null, BitmapColor? textColor = null);
}
```

Keep the renderers thin. Every *decision* (which text, how it wraps onto lines, which font size, which colour) goes in a pure, SDK-free, fully tested helper, e.g. a `ComicTitleFormatter` that wraps a title into N lines of at most M characters, or a `ButtonFontSize` that picks a size from line count and length. The renderer only paints what it's given (see `structure.md` pattern 5). If a renderer fails, log it and return `null`; never throw into the SDK.

### Example

```csharp
protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
{
    var comic = XkcdPlugin.Instance?.CurrentComic;
    if (comic == null)
    {
        return ButtonTextRenderer.RenderText("xkcd", imageSize, BitmapColor.Black, BitmapColor.White);
    }

    var text = ComicTitleFormatter.ForButton(comic.Num, comic.Title);
    return ButtonTextRenderer.RenderText(text, imageSize, BitmapColor.Black, BitmapColor.White);
}
```

## Drawing Comic Images

xkcd comic images are far larger than a button (roughly 80-120 px square on most devices), and many are wide strips that are unreadable when shrunk. Before building "show the comic on a button", consider: download the image off the SDK thread, decode and scale it once per comic (not per render), cache the result, and choose between crop and fit deliberately. Check the SDK docs for how `BitmapImage` accepts image bytes rather than assuming.

## Resource Naming

The helpers pass the resource name to `PluginResources.ReadImage`, which resolves it with `PluginResources.FindFile`, a **filename-only** lookup. Don't use a namespace prefix:

```csharp
ButtonImageHelper.Icon("Random.svg");                       // correct
ButtonImageHelper.Icon("Loupedeck.XkcdPlugin.Random.svg");  // wrong - and breaks if the assembly is renamed
```

Icons live in `src/Resources/Icons/`, each with its own `<EmbeddedResource>` entry in `src/XkcdPlugin.csproj`. Name status icon sets `<CommandName><State>.svg`.

## Accessibility

Colour is never the only signal. Pair a status colour with a distinct icon shape or text, so colour-blind users get the same information.

## When GetCommandImage Is Called

- When the button first appears
- After you request a redraw (see `icon-update-patterns.md` for which call on which base class)

The framework caches the result, so don't add your own image cache for *rendered buttons*. A cache for *downloaded source images* is a different thing and is fine.
