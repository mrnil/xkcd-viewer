# Product

## What this is

A Logi Actions SDK plugin that shows the current xkcd comic **across the keys** of the device: one comic split over a 3×2 block of keys, with navigation keys above it. As far as we know, no existing plugin does this.

It's an at-a-glance view. xkcd is text-heavy, and speech bubbles and labels will usually be unreadable at key size, so reading a comic properly means opening it in the browser, which is one key press away.

Everything under "Hardware facts" was **confirmed on real devices** (MX Creative Keypad, Loupedeck Live S, Loupedeck CT) with a throwaway spike on branch `spike/keypad-grid`, 2026-09-26/27. That branch's `GridSpikeFolder.cs` is a working reference for every mechanism below. The real folders were built fresh on `main`, with tests; the spike branch is kept for reference only and must never be merged.

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
- **`--`**: blank padding keys that keep the 3×2 block in one piece on wider grids. They're drawn as plain black keys. **A folder key must never get a null image**: on the Live S and CT, returning null for the padding keys showed the device's "action not available" warning triangle (seen on hardware 2026-10-11). So every fallback in `GetCommandImage` draws a blank key instead.
- **Dial** (where the device has one): fast scrolling through comics. Later.

## Hardware facts

### Detecting the device

There is **no API for the number of slots on a page**, but the SDK passes the actual device to `GetButtonPressActionNames(DeviceType deviceType)`. `DeviceTypeAliases` maps each device to its own `DeviceType` bit:

| Device | `DeviceType` | Alias | Grid | SDK Back key |
| --- | --- | --- | --- | --- |
| MX Creative Keypad | `Loupedeck70` (0x10000) | `MxCreativeKeypad` | 3 × 3 | grid slot 0 |
| Loupedeck Live S | `Loupedeck50` (0x800) | `LoupedeckLiveS` | 5 × 3 | grid slot 0 |
| Loupedeck CT | `Loupedeck20` (0x100) | `LoupedeckCt` | 4 × 3 | grid slot 0 |
| **Anything else** | | | **assume 3 × 3** | assume grid slot 0 |

Confirmed on hardware for the three listed devices. Other aliases exist (`LoupedeckLive` = `Loupedeck30`, `RazerStreamController` = `Loupedeck40`, `RazerStreamControllerX` = `Loupedeck60`, `MxCreativeDialpad` = `Loupedeck71`), but their grids haven't been observed, so they use the 3×3 fallback until someone tests one. Log the `DeviceType` whenever the fallback is used.

The per-device table is `src/xkcdviewerPlugin/Helpers/DeviceGrid.cs` (`IsKnown` tells the folder when to log the fallback). The slot list it produces (controls, padding, comic tiles) and the action names are `src/xkcdviewerPlugin/Helpers/FolderLayout.cs`, which takes only a column count.

### Key order and paging

The names returned by `GetButtonPressActionNames` fill the grid **left to right, top to bottom**, at the device's real width, **after** the SDK's Back key. Return exactly `columns × 3 − 1` names. One more and the SDK silently starts a second page, which the user reaches with the keypad's `<` `>` buttons.

### Navigation

Override `GetNavigationArea(DeviceType _)` and return `PluginDynamicFolderNavigation.ButtonArea`. The `Navigation` property is **obsolete** (compiler warning CS0618).

- **`ButtonArea`**: the SDK Back key takes **grid slot 0 on all three devices**.
- **`None`** doesn't remove the Back key, even though the docs say navigation is then "fully done by the plugin developer". It stays in slot 0 on the keypad and Live S. On the CT it moves to the left side strip, which is the one difference, and it makes the layout vary by navigation mode. Don't use `None`.
- `PluginDynamicFolder.NavigateUpActionName` (static, value `$@Generic___@DynamicFolderGoUp`) adds a *second* Back key. We don't need it.

### Captions: return an empty display name

By default, a folder key shows our image **shrunk into the top two-thirds, with the display name drawn as a caption underneath**. Folders have no `IsWidget`. The fix: **`GetCommandDisplayName` returns `String.Empty`** for every key, and the image then fills the whole key. Every key draws its own text into its image. The key that **opens** the folder is the same: **`GetButtonDisplayName` returns `String.Empty`**, and its caption goes (confirmed on the keypad 2026-10-11). `DisplayName` still names the action in Options+.

### Action group: always set one

A dynamic folder with no `GroupName` is listed in Options+ under a generic **"Control Center"** group, not directly under the plugin (tried 2026-10-11). `PluginApi.dll` has no setting to change that; the fallback comes from the Logi software. So `ComicFolder` keeps `GroupName = "xkcd viewer"`, even with only one or two actions.

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

Build the scaled source once per comic and tile size. `ComicFolder` caches that whole area privately (7 comics, matching the image cache) and cuts a fresh tile from it on each draw with the negative-offset `DrawImage`. It doesn't cache the tiles themselves: `BitmapImage` is `IDisposable` and the SDK docs don't say who owns an image once it has been returned, so no image handed to the SDK is ever reused or disposed by the plugin.

### Redraw

`CommandImageChanged(actionParameter)` for each key updates **all tiles at the same moment**, with no visible tearing. A folder has no `ActionImageChanged`. `ButtonActionNamesChanged()` is only for when the set of keys changes.

## Data

The data comes from `https://xkcd.com/info.0.json` (latest) and `https://xkcd.com/{num}/info.0.json`, through `IxkcdClient` (`src/xkcdviewerPlugin/Services/`). Decode the image bytes with `BitmapImage.FromArray(bytes)`. See `secure-coding.md` → "This Plugin" for how to treat those responses.

What real responses showed (captured as test fixtures in `tests/xkcdviewerPlugin.Tests/Fixtures/`):

- `num` is a JSON number. `month`, `day` and `year` are strings. They're parsed into `xkcdComic.Published` (null if missing or invalid), which only the new-comic check uses: it stops checking once the latest comic is dated today (see "Checking for new comics").
- `title` can hold HTML tags (#472) and entities (#259). `safe_title` is clean but can lose characters (#259's "Clichd"). So the title shown is `title` with tags stripped and entities decoded, falling back to `safe_title`, then to `#num`.
- Interactive comics (#1608, #1663) have `img` set to `https://imgs.xkcd.com/comics/` with no file name. They load with no image and get the title card.
- Comic 404 is a joke: `https://xkcd.com/404/info.0.json` answers HTTP 404 with an HTML page, and that answer *is* the comic. Prev/Next step onto it like any other number. `xkcdClient` turns that one HTTP 404 into comic 404 (title "404 Not Found", no image, so it gets the title card, and pressing a tile opens `https://xkcd.com/404/`). That is the only place an HTTP 404 counts as success: a 404 for any other comic, the latest comic or an image is a failure.

## Which comic, and caching

`ComicService` (`src/xkcdviewerPlugin/Services/`) holds which comic is showing and everything fetched so far. Decided 2026-10-11:

- **The folder opens at the last comic viewed.** The service takes it as a starting number and raises `Changed` whenever the comic changes, so the plugin can save it in the plugin settings and restore it next session. On a first run, or if the saved number isn't a comic, it opens at the latest. A saved number beyond the latest falls back to the latest.
- **Prev / Next are preloaded, two either side.** Whenever the current comic changes, the two comics before and after it are fetched in the background, details and image, so even two quick presses usually show their comic at once. After one press only one comic is newly in range, so a step costs one comic fetch. Only one fetch per comic runs at a time, however many keys or presses ask for it.
- **Comic details are kept for the whole session** (a published comic never changes, and each is about 200 bytes). **Image bytes are kept for the 7 most recently used comics** (decided 2026-10-11): the five preloaded plus one spare each way, so turning straight back after a step fetches nothing. A 16 MB total acts as a safety net for unusually large images; seven typical comics come to about 1 MB. Memory only: nothing is written to disk.
- **A failed fetch is retried only after 30 s** (`PluginTimings.FailedLoadRetryDelay`), and only when the comic is needed again, so pressing Prev / Next while offline doesn't send a request each time. If a comic's details arrive but its image doesn't, it shows as the title card.
- **"Latest" always means the newest comic published.** It's where Next stops, the starting comic on a first run, and the upper bound for random picks. Browsing fetches it once per session (and the hourly release-day check updates it; see "Checking for new comics"). **Showing the latest** (the Latest Comic folder, or holding Next) **asks xkcd again every time** (decided 2026-10-11), so it lands on a comic published since the session started, and Next's limit moves up with it. If asking again fails, the latest already known is shown; if none is known, "offline". Browsing while the latest is unknown still shows the last-viewed comic, and Next asks for the latest again (after the 30 s wait) instead of moving.
- **"Last viewed" is the comic showing**, whichever folder put it there: the Comic Browser, Latest Comic, Random Comic, or a hold on Prev / Next. It's what the Comic Browser opens at and what's saved across sessions.
- **Latest and random jumps** (`ShowLatestAsync`, `ShowRandomAsync`; decided 2026-10-11) wait for xkcd's answer about the latest comic. Until it arrives they show no comic ("loading…") rather than the last-viewed one, which would look like the answer. Opening the Comic Browser while one is waiting still shows the last-viewed comic. `ShowFirstAsync` (holding Prev) needs nothing first.
- **A random comic preloads the next random pick**, not its neighbours: its neighbours are no likelier to be shown next than any other comic. The pick is any comic from 1 to the latest except the one showing (`ComicNumbers.PickRandom`), so "show another" never repeats the comic in front of you. Comic 404 can come up like any other. Pressing Prev / Next after a random comic (only possible from another folder) goes back to preloading neighbours.

The cut tiles are cached by the folder itself, because they're SDK `BitmapImage`s.

## Checking for new comics

Decided and built 2026-10-11: `ReleaseSchedule` (when), `NewComicPoller` (the loop), `ComicService.RefreshLatestAsync` (the request). xkcd publishes on **Monday, Wednesday and Friday** (#3280–#3309, 3 Aug to 9 Oct 2026, had no gaps), but the API gives only a date. The one release time measured, from the latest `info.0.json`'s nginx ETag (`"<hex mtime>-<hex size>"`), was #3309 at 20:37 UTC (16:37 Eastern) on its listed date. `info.0.json` is CDN-cached for 5 minutes (`Cache-Control: max-age=300`).

**The plugin checks once an hour, on release days and in US afternoon hours**, while it's loaded, whether the folder is open or not. The aim is to find a new comic within about two hours without many installations asking xkcd at the same moment:

- **Days:** Monday, Wednesday and Friday, as the day in **US Eastern time** (`America/New_York`, following its EST/EDT change), not on the user's computer.
- **Hours:** noon to 23:00 Eastern. The last check is in the 22:00–22:59 hour, so at most 11 checks a day.
- **Minute:** each installation checks at one random minute and second past the hour, chosen **each time the plugin loads** and kept until it unloads. Each install then asks steadily once an hour, and installs are spread across the hour.
- **Stop once found:** once the latest comic known is dated today (Eastern), no more checks until the next release day. Most release days then cost one to four requests.
- **No retries:** a failed check (offline, xkcd down) does nothing more; the next hour's slot is the next try.
- **On load, always ask for the latest.** If it's newer than the latest known when the plugin last ran (saved in the plugin settings as `LatestKnownComic`, beside `LastViewedComic`), that counts as a new comic found, exactly as if a poll had found it. This also covers comics published outside the window or while the computer was off.
- **Key presses still ask xkcd directly:** Latest Comic and holding Next are unchanged (see "Which comic, and caching"); they're spread out by people pressing keys at different times.
- **No setting to turn polling off.**
- **What a new comic does**: see "The new-comic signal".

## The new-comic signal

Decided and built 2026-10-11: `NewComicTracker` (what's unread, the flash), `NewComicCommand` (the New Comic key), and the Comic Browser's Next key (`KeyContent`, `ComicFolder`). The plugin saves `NewestShownComic` and `LatestKnownComic` in its settings, beside `LastViewedComic`; `NewComicLookChanged` tells the keys to redraw, every 0.5 s while flashing.

- **Unread:** there's an unread comic while the newest comic published is newer than the **newest comic shown**, which is saved in the plugin settings. On a first run nothing is unread: the latest at that moment counts as shown.
- **Shown** means it appeared on the keypad, as its picture or as its title card, by any route: Next, Latest Comic, a hold on Next, or Random Comic happening to land on it. Opening it in the browser doesn't count, and nor does seeing its number while it's loading or offline.
- **When a new comic is found** (by polling, or by the check at load finding one newer than the newest shown): the **New Comic** key and the Next chevron flash between their normal look and green for **30 s**, then stay green until nothing is unread (see "Where it shows"; the folder keys can't change). A key turned green at load after a restart, with nothing newly found, is solid green, not flashing.
- **"new" label:** a green key also shows "new", so the signal doesn't rely on colour alone. On Latest Comic it replaces the "latest" banner across the logo.
- **Green:** `#15B01A`, "green" in xkcd's colour-name survey (xkcd.com itself has no green; its accents are blue-greys, `#96A8C8` and `#6E7B91`). Confirmed by the owner from a preview, 2026-10-11, for both the key background and the chevron.
- **Opening the Comic Browser** always goes to the last comic viewed and ignores anything unread (owner, 2026-10-11; this replaced the earlier rule, made when the folder keys were to turn green, that it opens at the first unread comic). The green Next chevron is how to get to the new comic from there.
- **Inside the Comic Browser:** while something is unread, the Next chevron flashes green (30 s) then stays green. **Tapping** a green Next skips the normal step and goes to the first unread comic, the one after the newest shown, even if several are unread. **Holding** it still jumps to the very newest comic.
- **Redrawing the folder key:** the SDK has no documented call for it. `Plugin.OnActionImageChanged` with the folder's `CommandName` or `Name` didn't re-invoke `GetButtonImage`; telling the service the plugin's actions changed (`PluginActionsChanged(true, false)`, protected on `Plugin`) did, within about 150 ms (2026-10-11, branch `spike/folder-key-flash`). **But the keypad never showed the new image**: each re-list showed a white warning triangle on all three folder keys for about half a second, then the unchanged logo (seen on the keypad 2026-10-11). So, as far as we know, **a folder key's image can't change after it's first drawn**, and the folder keys can't flash or turn green. The signal needs another design; see below.

A green key is a **green background** behind the black logo, not a frame (owner, 2026-10-11). The logo SVG has a transparent background, so it draws straight onto the green (checked off-device).

**Where it shows** (owner, 2026-10-11, since the folder keys can't change):

- **A new "New Comic" command key** in the xkcd viewer group, placed by the user wherever they like. It's a `PluginDynamicCommand`, so `ActionImageChanged()` can redraw it. With nothing unread it shows the logo with its **eyes closed** (`art/xkcdviewerlogo-closed.svg`; owner, 2026-10-11); when a comic is found it flashes between that and the **open-eyed** logo on green with a "new" banner for 30 s, then stays green until nothing is unread. **With nothing unread, pressing it opens the latest comic in a browser tab** (owner, 2026-10-11). **While something is unread, pressing it opens the first unread comic in a browser tab** (owner, 2026-10-11). That doesn't count as shown, so the key stays green until the comic appears on the keypad.
- **The Next chevron** inside the folders, as above.
- The folder keys themselves stay as they are.

Only the **Comic Browser**'s Next chevron goes green; Latest Comic's stays as it is (owner, 2026-10-11).

## The folders

Three folder actions, all in the **xkcd viewer** group and all the same folder underneath (`ComicFolder` in a `ComicFolderMode`; decided 2026-10-11):

| Folder action | Class | Opens at | Top-row keys |
| --- | --- | --- | --- |
| **Comic Browser** | `ComicFolder` | The last comic viewed | Prev / Next |
| **Latest Comic** | `LatestComicFolder` | The newest comic published (asks xkcd each time) | Prev / Next |
| **Random Comic** | `RandomComicFolder` | A random comic, new each time it opens | One "show another" key, where Next is; Prev's key is blank |

Every folder key shows the logo at the same size, with a white margin of a tenth of the key all round. The Latest and Random keys add a diagonal banner across it, "latest" or "random" in white on xkcd's blue-grey (`LogoBanner`; chosen by the owner 2026-10-11 from previews), so the three can be told apart. Whatever a folder shows becomes the last comic viewed, so the Comic Browser picks up where Latest or Random left off. The two new folders subclass `ComicFolder` rather than sharing an abstract base, because the SDK docs don't say whether abstract action classes are skipped.

The rest of this section describes the Comic Browser; the other two differ only as in the table.

- **Getting there:** the user places the **Comic Browser** folder action (in the **xkcd viewer** group in Options+; named 2026-10-11) on any key. That key shows the owner's own xkcdviewer logo (a hand-lettered "xkcd" over "viewer", with the eyes underneath, `art/xkcdviewerlogo.svg`; laid out that way by the owner on 2026-10-11, from an earlier eyes-over-"xkcd" version. That logo replaced the owner's earlier pencil drawing (`art/xkcdviewer-logo.jpg`, since deleted; it's in git history) on 2026-10-11, which had itself replaced the official xkcd logo that day, for licensing) on white. The plugin embeds that SVG and draws it at each key's own size (`KeyRenderer.Logo`, with `VectorImage`; decided 2026-10-11, following the owner's Streaming Assistant plugin), so it's sharp at every size. The package icon, which must be a PNG, is just the logo's eyes, with no lettering (`art/xkcdviewer-icon.svg`; owner, 2026-10-11), made by `art/make-icons.ps1` on a white rounded tile, because the line art alone vanishes on Options+'s black background. A comic thumbnail on it was ruled out: the owner has later plans it wouldn't suit. Pressing it opens the folder, which starts the service (`Activate()`), at the last comic viewed.
- **Prev / Next:** a chevron (one rounded SVG stroke, `Chevron`; redrawn 2026-10-11 because two separate lines looked blocky and notched at the tip), with no comic number. Dimmed when there's nowhere to go. A tap steps one comic. **Holding one for 2 s** (`PluginTimings.HoldToJump`; decided 2026-10-11) jumps instead: Next to the newest comic published (as Latest Comic does), Prev to comic #1. The jump happens while the key is still held, and letting go afterwards does nothing more. This needs the key's press and release: the MX Keypad's physical keys send them to `ProcessButtonEvent2`, and the Live S / CT touch keys, which send no button events, to `ProcessTouchEvent` as `Press` and `TouchUp` (or `LongRelease`). `KeyHold` tells a tap from a hold either way. On the touch keys the device decides when a touch is a hold: it sends `LongPress` about **0.5 s** in, with haptic feedback, and the jump happens then (`KeyHold.TryHoldNow`). Waiting for the keypad's 2 s didn't work there: users let go soon after the feedback (holds of 0.9–1.8 s in the log), so the release counted as a tap. Taps therefore step on release, not on press. A key that sends neither still steps through `RunCommand`, but can't be held. **History (2026-10-11):** the hold first listened only for button events, so it did nothing on the Live S / CT. A double tap (the device's own `DeviceTouchEventType.DoubleTap`) was tried there instead, but the device counts a second tap within about half a second as a double tap, so stepping quickly jumped to the end; the owner chose the hold on touch events instead. Latest Comic has the same keys, so the hold works there too.
- **Comic tiles:** one sixth of the comic each. While loading, the top-middle tile says "loading…" (yellow) and the bottom-middle one the comic number. When offline, it says "offline" (grey). Text never crosses a seam.
- **Title card** (no image, or a shape `ComicFit` rejects): "#num" on tile 0, and the title word-wrapped across tiles 1–5 by `TitleCardLayout` (3 lines of 9 characters per tile, long words hyphenated, an ellipsis if it overflows). Black on white, like xkcd.
- **A tile press** opens `https://xkcd.com/{num}/`, built from the number, so it works even while the comic is loading or offline. `BrowserLauncher` refuses any other URL. The SDK has no API for opening a URL, so it uses the OS (`open` on macOS, shell execute on Windows).
- **Last viewed** is saved as the plugin setting `LastViewedComic`, locally only, whenever the comic changes.

### Hardware test checklist

- The new-comic signal: the New Comic key (closed eyes; on a new comic it flashes green with "new" for 30 s, then stays green; a press opens the first unread comic, or the latest when nothing is unread), and the Comic Browser's Next chevron (green while something is unread; a tap goes to the first unread comic, a hold to the newest). Confirmed on the keypad 2026-10-11 with two comics faked as unread. Still to see: a real new comic found by a scheduled check, on the next release day; the log says when ("Next check for a new comic at ...")
- The layout on each device (keypad 3×3, Live S 5×3, CT 4×3), and the folder key's logo (legible at 80 px?)
- Prev/Next feel instant; the chevrons dim at the first and latest comics
- Real comics in tiles: ordinary strips, tall ones (#1732), huge ones (#657), interactive ones (#1663 → title card), and #404. This tunes the `ComicFit` thresholds and `TitleCardLayout`'s 9×3 text size
- Loading and offline placeholders (disconnect the network)
- The last-viewed comic survives a plugin restart
- A tile press opens the right page
- Latest Comic opens at the newest comic; Random Comic opens at a different comic each time, and its one "show another" key changes it (quickly, as the next one is preloaded)
- "show another" fits its key (the folder keys' logo, margin and "latest" / "random" banners were confirmed on the keypad 2026-10-11: sharp, clear of the edges, readable. The logo has since been redrawn as "xkcd" / "viewer" / eyes, so recheck it at 80 px, and whether the banner hiding "viewer" is acceptable)
- Prev / Next: a tap steps exactly one comic (not two: if the SDK also calls `RunCommand`, a press could count twice); holding 2 s jumps to #1 / the newest while still held; letting go after the jump doesn't step. On the Live S / CT, the same by touch: quick taps each step once, a hold jumps at the device's long-press feedback, and lifting after the jump doesn't step (all confirmed on hardware 2026-10-11)

## Comic shapes

The comic area is 3:2 landscape. Most comics fit it well. For the rest:

- **Tall or square comics**: fit into the area and letterbox (black bars). Wasted space is acceptable.
- **Very large or very tall comics** (e.g. multi-panel epics, interactive comics): even the thumbnail is meaningless. Show the title and number across the tiles instead, with "open in browser" still working.
- Deciding which case applies is `src/xkcdviewerPlugin/Helpers/ComicFit.cs`, from the image's width and height. A comic gets the title card if it would be shrunk below **0.2×** to fit, or if its shape would cover less than **a quarter** of the area (taller than about 3:8, or wider than 6:1). Both thresholds are first guesses: check them on hardware against real comics and adjust.
- A comic smaller than the area is scaled up to fit it.

## Still open

1. **Manifest `supportedDevices`**: lists only `LoupedeckCtFamily`, yet the folder appeared on the MX Creative Keypad during development (loaded through the `.link` file), and the finished plugin still works there loaded the same way (2026-10-11). That says nothing about a packaged install. A packed `.lplug4` passes `LogiPluginTool verify` (2026-10-11), so what's left before the first release is a packaged install (`LogiPluginTool install`, after `dotnet clean`) to confirm the keypad still gets it, and whether the manifest should also list `LogitechCreativeFamily` / `MxCreativeKeypad`. The manifest also sets `pluginFolderMac`, but the plugin hasn't been built or run on macOS.
2. **Accidental presses**: every comic tile opens the browser (decided), so watch in use whether stray presses are a nuisance.

## v1 scope

- The folder with the 3×2 comic block and the Prev / Next row, laid out per device as above, placeable as Comic Browser, Latest Comic or Random Comic (see "The folders")
- The last comic viewed on open (the latest on a first run); Prev/Next by number (comic 404 included: it's a joke, see "Data"), held 2 s to jump to the first / newest; a comic tile press opens it in the browser
- Comics, images and sliced tiles cached in memory, with Prev / Next preloaded (see "Which comic, and caching")
- A sensible placeholder while loading and when offline
- New comics noticed on release days, shown on a New Comic key and the Comic Browser's green Next chevron (see "Checking for new comics" and "The new-comic signal")

## Later (not committed)

- **Free the keypad's top row for the comic** (owner, 2026-10-11): use the MX Creative Keypad's own physical buttons, and/or an adjustment (dial) for navigation, as Prev / Next and other actions, so more of the 3×3 display shows the comic. The SDK's Back key in slot 0 can't be removed (see "Navigation"), so check what's possible before designing: whether a folder can receive those buttons, and whether a dial can drive it.
- **A bigger comic on bigger devices** (owner, 2026-10-11): the Live S (5×3) and CT (4×3) could show the comic over more keys than 3×2, to make it more readable. The rule that text must not cross a seam, and the Back key in slot 0, still apply.
- Use the spare top-row keys on wider grids (alt text)
- Alt text view
- Favourites
- A dial for fast scrolling where one is available
