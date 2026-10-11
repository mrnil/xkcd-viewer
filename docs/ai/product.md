# Product

## What this is

A Logi Actions SDK plugin that shows the current xkcd comic **across the keys** of the device: one comic split over a 3×2 block of keys, with navigation keys above it. As far as we know, no existing plugin does this.

It's an at-a-glance view. xkcd is text-heavy, and speech bubbles and labels will usually be unreadable at key size, so reading a comic properly means opening it in the browser, which is one key press away.

Everything under "Hardware facts" was **confirmed on real devices** (MX Creative Keypad, Loupedeck Live S, Loupedeck CT) with a throwaway spike on branch `spike/keypad-grid`, 2026-09-26/27. That branch's `src/Actions/GridSpikeFolder.cs` is a working reference for every mechanism below. Don't merge it; build the real folder fresh, with tests.

## Layout

The SDK always puts its own Back key in the top-left grid slot (see "Navigation"), so the controls go on the top row and the comic gets the two rows below:

```text
MX Creative Keypad (3×3)        Loupedeck Live S (5×3)              Loupedeck CT (4×3)
[Back][Prev][Next]              [Back][Prev][Next][ -- ][ -- ]      [Back][Prev][Next][ -- ]
[ c0 ][ c1 ][ c2 ]              [ -- ][ c0 ][ c1 ][ c2 ][ -- ]      [ c0 ][ c1 ][ c2 ][ -- ]
[ c3 ][ c4 ][ c5 ]              [ -- ][ c3 ][ c4 ][ c5 ][ -- ]      [ c3 ][ c4 ][ c5 ][ -- ]
```

- **Back**: inserted by the SDK. We don't return it.
- **Prev / Next**: the previous and next comic.
- **Comic tiles `c0`–`c5`**: pressing **any** of them opens the current comic in the browser (`https://xkcd.com/{num}/`). Decided 2026-09-27: there's no dedicated "Open" key.
- **`--`**: blank padding keys that keep the 3×2 block in one piece on wider grids. They show the device's default empty-key grey, not a drawn colour, which is fine.
- **Dial** (where the device has one): fast scrolling through comics. Later.

## Hardware facts

### Detecting the device

There is **no API for the number of slots on a page**, but the SDK passes the actual device to `GetButtonPressActionNames(DeviceType deviceType)`. `DeviceTypeAliases` maps each device to its own `DeviceType` bit:

| Device | `DeviceType` | Alias | Grid | SDK Back key |
|---|---|---|---|---|
| MX Creative Keypad | `Loupedeck70` (0x10000) | `MxCreativeKeypad` | 3 × 3 | grid slot 0 |
| Loupedeck Live S | `Loupedeck50` (0x800) | `LoupedeckLiveS` | 5 × 3 | grid slot 0 |
| Loupedeck CT | `Loupedeck20` (0x100) | `LoupedeckCt` | 4 × 3 | grid slot 0 |
| **Anything else** | | | **assume 3 × 3** | assume grid slot 0 |

Confirmed on hardware for the three listed devices. Other aliases exist (`LoupedeckLive` = `Loupedeck30`, `RazerStreamController` = `Loupedeck40`, `RazerStreamControllerX` = `Loupedeck60`, `MxCreativeDialpad` = `Loupedeck71`), but their grids haven't been observed, so they use the 3×3 fallback until someone tests one. Log the `DeviceType` whenever the fallback is used.

The per-device table is `src/Helpers/DeviceGrid.cs` (`IsKnown` tells the folder when to log the fallback). The slot list it produces (controls, padding, comic tiles) and the action names are `src/Helpers/FolderLayout.cs`, which takes only a column count.

### Key order and paging

The names returned by `GetButtonPressActionNames` fill the grid **left to right, top to bottom**, at the device's real width, **after** the SDK's Back key. Return exactly `columns × 3 − 1` names. One more and the SDK silently starts a second page, which the user reaches with the keypad's `<` `>` buttons.

### Navigation

Override `GetNavigationArea(DeviceType _)` and return `PluginDynamicFolderNavigation.ButtonArea`. The `Navigation` property is **obsolete** (compiler warning CS0618).

- **`ButtonArea`**: the SDK Back key takes **grid slot 0 on all three devices**.
- **`None`** doesn't remove the Back key, even though the docs say navigation is then "fully done by the plugin developer". It stays in slot 0 on the keypad and Live S. On the CT it moves to the left side strip, which is the one difference, and it makes the layout vary by navigation mode. Don't use `None`.
- `PluginDynamicFolder.NavigateUpActionName` (static, value `$@Generic___@DynamicFolderGoUp`) adds a *second* Back key. We don't need it.

### Captions: return an empty display name

By default, a folder key shows our image **shrunk into the top two-thirds, with the display name drawn as a caption underneath**. Folders have no `IsWidget`. The fix: **`GetCommandDisplayName` returns `String.Empty`** for every key, and the image then fills the whole key. Every key draws its own text into its image.

### Slicing: Crop, no gap

Tile sizes seen: `Width116` = **116 × 116 px** and `Width90` = **80 × 80 px**. Always slice for the `imageSize` that `GetCommandImage` is given.

Three slicing methods were compared on the devices. **Crop with no gap looks best**:

```csharp
using (var builder = new BitmapBuilder(sourceWidth, sourceHeight))   // source = the whole 3×2 area
{
    builder.DrawImage(source, 0, 0);
    builder.Crop(column * tileWidth, row * tileHeight, tileWidth, tileHeight);
    tile = builder.ToImage();
}
```

A negative-offset `DrawImage` gives the same geometry. Skipping a strip at each seam (a "grille") looked worse. The picture reads as continuous across the physical gaps on all three devices, but **text crossing a seam loses letters**, so never draw anything that must stay readable across a seam.

Build the scaled source once per comic and tile size, and cache the six tiles. `BitmapBuilder` is `IDisposable`.

### Redraw

`CommandImageChanged(actionParameter)` for each key updates **all tiles at the same moment**, with no visible tearing. A folder has no `ActionImageChanged`. `ButtonActionNamesChanged()` is only for when the set of keys changes.

## Data

The data comes from `https://xkcd.com/info.0.json` (latest) and `https://xkcd.com/{num}/info.0.json`, through `IxkcdClient` (`src/Services/`). Decode the image bytes with `BitmapImage.FromArray(bytes)`. See `secure-coding.md` → "This Plugin" for how to treat those responses.

What real responses showed (captured as test fixtures in `tests/xkcdPlugin.Tests/Fixtures/`):

- `num` is a JSON number. `month`, `day` and `year` are strings. Nothing uses the dates yet.
- `title` can hold HTML tags (#472) and entities (#259). `safe_title` is clean but can lose characters (#259's "Clichd"). So the title shown is `title` with tags stripped and entities decoded, falling back to `safe_title`, then to `#num`.
- Interactive comics (#1608, #1663) have `img` set to `https://imgs.xkcd.com/comics/` with no file name. They load with no image and get the title card.
- Comic 404 is a joke: `https://xkcd.com/404/info.0.json` answers HTTP 404 with an HTML page, and that answer *is* the comic. Prev/Next step onto it like any other number. `xkcdClient` turns that one HTTP 404 into comic 404 (title "404 Not Found", no image, so it gets the title card, and pressing a tile opens `https://xkcd.com/404/`). That is the only place an HTTP 404 counts as success: a 404 for any other comic, the latest comic or an image is a failure.

## Which comic, and caching

`ComicService` (`src/Services/`) holds which comic is showing and everything fetched so far. Decided 2026-10-11:

- **The folder opens at the last comic viewed.** The service takes it as a starting number and raises `Changed` whenever the comic changes, so the plugin can save it in the plugin settings and restore it next session (step 3). On a first run, or if the saved number isn't a comic, it opens at the latest. A saved number beyond the latest falls back to the latest.
- **Prev / Next are preloaded, two either side.** Whenever the current comic changes, the two comics before and after it are fetched in the background, details and image, so even two quick presses usually show their comic at once. After one press only one comic is newly in range, so a step costs one comic fetch. Only one fetch per comic runs at a time, however many keys or presses ask for it.
- **Comic details are kept for the whole session** (a published comic never changes, and each is about 200 bytes). **Image bytes are kept for the 7 most recently used comics** (decided 2026-10-11): the five preloaded plus one spare each way, so turning straight back after a step fetches nothing. A 16 MB total acts as a safety net for unusually large images; seven typical comics come to about 1 MB. Memory only: nothing is written to disk.
- **A failed fetch is retried only after 30 s** (`PluginTimings.FailedLoadRetryDelay`), and only when the comic is needed again, so pressing Prev / Next while offline doesn't send a request each time. If a comic's details arrive but its image doesn't, it shows as the title card.
- **The latest comic is fetched once per session.** It's where Next stops, and the starting comic on a first run. If that fetch fails, the last-viewed comic still shows, and Next asks for the latest again (after the 30 s wait) instead of moving.

The cut tiles are cached by the folder itself (step 3), because they're SDK `BitmapImage`s.

## Comic shapes

The comic area is 3:2 landscape. Most comics fit it well. For the rest:

- **Tall or square comics**: fit into the area and letterbox (black bars). Wasted space is acceptable.
- **Very large or very tall comics** (e.g. multi-panel epics, interactive comics): even the thumbnail is meaningless. Show the title and number across the tiles instead, with "open in browser" still working.
- Deciding which case applies is `src/Helpers/ComicFit.cs`, from the image's width and height. A comic gets the title card if it would be shrunk below **0.2×** to fit, or if its shape would cover less than **a quarter** of the area (taller than about 3:8, or wider than 6:1). Both thresholds are first guesses: check them on hardware against real comics and adjust.
- A comic smaller than the area is scaled up to fit it.

## Still open

1. **Manifest `supportedDevices`**: lists only `LoupedeckCtFamily`, yet the folder appeared on the MX Creative Keypad during development (loaded through the `.link` file). Before the first release, confirm with `LogiPluginTool verify` and a packaged install that the keypad still gets it, and whether the manifest should also list `LogitechCreativeFamily` / `MxCreativeKeypad`.
2. **Accidental presses**: every comic tile opens the browser (decided), so watch in use whether stray presses are a nuisance.
3. **Checking for new comics**: on hold (2026-10-11) while the owner thinks it over. Today the latest comic is fetched once per session and never re-checked, so a comic published while the plugin runs only appears after a restart. The proposal was to re-check when the folder opens and when Next is pressed on the newest comic, at most every 5 minutes, with no background timer.

## v1 scope

- The folder with the 3×2 comic block and the Prev / Next row, laid out per device as above
- The last comic viewed on open (the latest on a first run); Prev/Next by number (comic 404 included: it's a joke, see "Data"); a comic tile press opens it in the browser
- Comics, images and sliced tiles cached in memory, with Prev / Next preloaded (see "Which comic, and caching")
- A sensible placeholder while loading and when offline

## Later (not committed)

- Use the spare top-row keys on wider grids (random comic, alt text)
- Alt text view
- Favourites
- A dial for fast scrolling where one is available
