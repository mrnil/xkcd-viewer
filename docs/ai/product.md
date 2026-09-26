# Product

## What this is

A Logi Actions SDK plugin that shows the current xkcd comic **across the keys** of the device: one comic split over a block of tiles, with navigation keys beside it. As far as we know, no existing plugin does this.

It's an at-a-glance view. xkcd is text-heavy, and speech bubbles and labels will usually be unreadable at key size, so reading a comic properly means opening it in the browser, which is one key press away.

## The grid: always 3×3

**The plugin can't detect which device it's running on, so it always assumes a 3×3 grid of keys**, on every device. There's no device-specific layout and no runtime detection: `GetButtonPressActionNames` always returns the same nine names. Ignore its `DeviceType` parameter (and the one on `GetNavigationArea`, etc.), and never branch on it.

Design and test on the **MX Creative Keypad**, which is exactly 3×3.

## Layout

The comic fills the top two rows, a 3:2 landscape area that suits most comics. The bottom row holds the controls:

```text
[ comic ][ comic ][ comic ]
[ comic ][ comic ][ comic ]
[ Prev  ][ Open  ][ Next  ]
```

- **Prev / Next**: the previous and next comic.
- **Middle key**: open the comic in the browser (`https://xkcd.com/{num}/`). Candidates for a long press or a later alternative: show the alt text, or go back out of the folder.
- **Dial** (where the device has one): fast scrolling through comics.

## How it works

A `PluginDynamicFolder` is a workspace whose keys the plugin fully controls. Verified against PluginApi 6.4.1.3246 by reflection:

1. **Keys**: override `GetButtonPressActionNames(DeviceType deviceType)` to return one name per key, built with `this.CreateCommandName("0")` … `this.CreateCommandName("8")`. `RunCommand(String actionParameter)` and `GetCommandImage(String actionParameter, PluginImageSize imageSize)` then get `"0"`…`"8"`. On a folder these overrides are `public`. Mapping a key index to its row and column (`index / 3`, `index % 3`), and the row to "comic tile" or "control", is pure logic with the 3×3 grid as a constant. Put it in a tested helper.
2. **Image slicing**: download the comic once (`BitmapImage.FromArray(bytes)`), scale it to fit the six-tile comic area, and cut out one tile per key. `BitmapBuilder` supports this: `new BitmapBuilder(imageSize)`, then `DrawImage(image, x, y, width, height)` with a negative offset to place the right part of the scaled comic in the tile (or `Crop(x, y, w, h)`), then `ToImage()`. **Cache the six tiles per comic and size**; never re-slice inside `GetCommandImage`.
3. **Redraw on change**: a folder has **no** `ActionImageChanged`. Call `this.CommandImageChanged(actionParameter)` once for each key (all nine when the comic changes). `ButtonActionNamesChanged()` is only for when the *set* of keys changes.
4. **Navigation**: `Navigation` (`PluginDynamicFolderNavigation`: `None`, `ButtonArea`, `EncoderArea`) has a **protected setter**, and `GetNavigationArea(DeviceType)` is virtual. The default navigation adds a **Back** key in the top-left corner, which would take comic tile 0. Setting `this.Navigation = PluginDynamicFolderNavigation.None` gives the plugin all nine keys, but then Home is the only built-in way out. `NavigateUpActionName` exists and can be returned from `GetButtonPressActionNames` to put our own Back key wherever we choose (e.g. the middle bottom key).
5. **Dial**: a folder's encoders are defined the same way, with `GetEncoderRotateActionNames(DeviceType)` and `ApplyAdjustment(String actionParameter, Int32 diff)`.

The data comes from `https://xkcd.com/info.0.json` (latest) and `https://xkcd.com/{num}/info.0.json`. See `secure-coding.md` → "This Plugin" for how to treat those responses.

## Risks and open questions: test these first, on hardware

Build a throwaway spike that answers these before building the real feature:

1. **Navigation on the MX Creative Keypad**: does the default Back key appear in tile 0 there? Does `Navigation = None` work as expected, and how does the user get out of the folder (Home, or our own `NavigateUpActionName` key)?
2. **Key order**: the SDK doesn't say which key an action name lands on. Confirm that the nine returned names fill the grid left to right, top to bottom.
3. **Tile size**: `PluginImageSize` comes in several sizes (`Width60`, `Width90`, `Width116`, and pixel variants). This isn't device detection: each `GetCommandImage` call is told the size to draw at. Slice for the size given, never an assumed one, and log what the keypad passes.
4. **Bezels**: the physical gaps between keys break up the picture. Try both a seamless slice and one that skips a strip at each seam (so the picture reads as if seen through a grille), and compare on the device. A sibling project that drew a 3×3 block across keys found that text hidden behind a gap is lost, so avoid placing anything that must stay readable across a seam.
5. **Redraw**: confirm that `CommandImageChanged` for all nine keys updates the whole block cleanly, without visible tearing between tiles.
6. **Manifest**: `supportedDevices` currently lists only `LoupedeckCtFamily`, and the SDK documents no value for the MX Creative Keypad. Find out what's needed for the plugin to appear on the keypad (`LogiPluginTool verify`, SDK docs, or Logi) before the first release.

## Comic shapes

Most comics are landscape and fit the 3:2 area well. For the rest:

- **Tall or square comics**: fit into the area and letterbox (black bars). Wasted space is acceptable.
- **Very large or very tall comics** (e.g. multi-panel epics, interactive comics): even the thumbnail is meaningless. Show the title and number across the tiles instead, with "Open" still working.
- The decision "which of these is it" is pure logic on the image's width and height. Put it in a tested helper (`structure.md` pattern 5).

## v1 scope

- The folder with the 6-tile comic and the Prev / Open / Next row
- Latest comic on open; Prev/Next by number (skipping comic 404, which doesn't exist)
- Comics and sliced tiles cached in memory; the latest comic number checked at most every few minutes
- A sensible placeholder while loading and when offline

## Later (not committed)

- Alt text on a key (long press on the middle key, or a separate view)
- A random-comic key
- Favourites
- A dial for fast scrolling where one is available
