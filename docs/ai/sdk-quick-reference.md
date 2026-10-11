# SDK Quick Reference

Templates for the common Logi Actions SDK shapes. For anything not covered here, consult the SDK docs index (<https://logitech.github.io/actions-sdk-docs/llms.txt>, see `AGENTS.md`) rather than guessing.

The templates are illustrations of each SDK shape: members such as `CurrentComic`, `ButtonTextRenderer`, `Navigate` or `Favourites` don't exist in this plugin. For the real thing, see `src/xkcdviewerPlugin/Actions/` (`ComicFolder` for a folder, `NewComicCommand` for an image-only command) and `KeyRenderer` for drawing.

The SDK discovers actions by reflection: any non-abstract `PluginDynamicCommand` / `PluginDynamicAdjustment` / `PluginDynamicFolder` / `ActionEditorCommand` subclass in the assembly with a public parameterless constructor appears in the action list. There is no registration file.

## Command Display Modes

A `PluginDynamicCommand` shows **either** text **or** an image it draws. Pick one per command.

### Text-Only Command

```csharp
public class ComicNumberDisplay : PluginDynamicCommand
{
    public ComicNumberDisplay()
        : base(displayName: "Comic Number", description: "Shows the current comic number", groupName: "Comics")
    {
    }

    // Override ONLY this. Do not set IsWidget, do not override GetCommandImage.
    protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        => $"xkcd{Environment.NewLine}#{xkcdviewerPlugin.Instance?.CurrentComic?.Num}";
}
```

### Image-Only Command (Widget)

```csharp
public class ComicTitleDisplay : PluginDynamicCommand
{
    public ComicTitleDisplay()
        : base(displayName: "Comic Title", description: "Shows the current comic title", groupName: "Comics")
    {
        this.IsWidget = true; // REQUIRED, or the display name is drawn over the image as a title
    }

    // Override ONLY this. Do not override GetCommandDisplayName.
    protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        => ButtonTextRenderer.RenderText(xkcdviewerPlugin.Instance?.CurrentComic?.Title ?? "xkcd", imageSize);
}
```

## Parameterized Command

One command class, many buttons: each parameter appears as its own action in the list.

```csharp
public class ComicNavigationCommand : PluginDynamicCommand
{
    public ComicNavigationCommand()
        : base()
    {
        this.AddParameter("previous", "Previous Comic", "Comics");
        this.AddParameter("next", "Next Comic", "Comics");
        this.AddParameter("random", "Random Comic", "Comics");
    }

    protected override void RunCommand(String actionParameter)
    {
        if (String.IsNullOrEmpty(actionParameter))
        {
            return;
        }

        xkcdviewerPlugin.Instance?.Navigate(actionParameter);
    }
}
```

To rebuild parameters at runtime: `RemoveAllParameters()`, `AddParameter(...)` for each, then `ParametersChanged()`.

## Adjustment (Dial)

```csharp
public class ComicBrowseAdjustment : PluginDynamicAdjustment
{
    public ComicBrowseAdjustment()
        : base(displayName: "Browse Comics", description: "Turn to move through comics", groupName: "Comics", hasReset: true)
    {
    }

    // diff > 0: clockwise, diff < 0: anticlockwise. Several ticks can arrive in one call.
    protected override void ApplyAdjustment(String actionParameter, Int32 diff)
    {
        xkcdviewerPlugin.Instance?.MoveBy(diff);
        this.AdjustmentValueChanged();
    }

    // With hasReset: true, pressing the dial (or the generated reset action) calls this.
    protected override void RunCommand(String actionParameter)
    {
        xkcdviewerPlugin.Instance?.GoToLatest();
        this.AdjustmentValueChanged();
    }

    protected override String GetAdjustmentValue(String actionParameter)
        => $"#{xkcdviewerPlugin.Instance?.CurrentComic?.Num}";
}
```

## Dynamic Folder

```csharp
public class FavouritesFolder : PluginDynamicFolder
{
    public FavouritesFolder()
    {
        this.DisplayName = "Favourites";
        this.GroupName = "Comics";
    }

    public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        => xkcdviewerPlugin.Instance?.Favourites.Select(n => this.CreateCommandName(n.ToString()))
           ?? Enumerable.Empty<String>();

    public override void RunCommand(String actionParameter)
    {
        if (String.IsNullOrEmpty(actionParameter))
        {
            return;
        }

        xkcdviewerPlugin.Instance?.OpenComic(actionParameter);
    }

    public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        => ButtonTextRenderer.RenderText($"#{actionParameter}", imageSize);
}
```

After the list changes, call `this.ButtonActionNamesChanged()`. Note that folder overrides are `public`, while the command/adjustment ones are `protected`. Check the exact signatures in the SDK docs before writing a folder.

## Plugin Lifecycle

```csharp
public class xkcdviewerPlugin : Plugin
{
    public override Boolean UsesApplicationApiOnly => true;
    public override Boolean HasNoApplication => true;

    public xkcdviewerPlugin()
    {
        // Helpers first: everything after may log or read resources.
        PluginLog.Init(this.Log);
        PluginResources.Init(this.Assembly);

        // No I/O here. This plugin also creates its services in Load, not here, so actions must null-check them.
    }

    public override void Load()
    {
        // Subscribe to events, start timers, kick off the first background fetch.
    }

    public override void Unload()
    {
        // Mirror Load exactly: unsubscribe, stop timers, cancel in-flight work, dispose.
    }
}
```

Plugin settings (small key/value persistence): `this.SetPluginSetting(key, value, backupOnline)` / `this.TryGetPluginSetting(key, out var value)` on `Plugin`. Keep them out of the services so the services stay testable: here the services take saved values as constructor arguments and raise events, and `xkcdviewerPlugin` reads and writes the settings (`LastViewedComic`, `NewestShownComic`, `LatestKnownComic`).

## Common Patterns

### Refresh After State Change

```csharp
this.ActionImageChanged(actionParameter); // one parameter
this.ActionImageChanged();                // the whole command
this.AdjustmentValueChanged();            // an adjustment's value text
```

### Async Operations From an SDK Callback

```csharp
protected override void RunCommand(String actionParameter)
{
    Task.Run(async () =>
    {
        try
        {
            await xkcdviewerPlugin.Instance.LoadRandomAsync();
            this.ActionImageChanged();
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Loading a random comic failed");
        }
    });
}
```

### Resource Loading

Embedded resources are looked up by **file name only** (`PluginResources.FindFile`), so no namespace prefix is needed:

```csharp
var icon = PluginResources.ReadImage("Random.svg");
```

The file must be an `<EmbeddedResource>` in `src/xkcdviewerPlugin/xkcdviewerPlugin.csproj`.

### Drawing an SVG Inside a Composed Key

Returning an SVG `BitmapImage` as a key's whole image is fine. To draw an SVG *into* a `BitmapBuilder` (with a label, a coloured background, and so on), render it at the exact size with `VectorImage`, which is public in `PluginApi.dll` but not in the SDK docs:

```csharp
var vector = new VectorImage();
if (vector.Load(svgXml))
{
    using (var image = vector.ToBitmap(size, size))
    {
        builder.DrawImage(image, x, y);
    }
}
```

`BitmapImage.FromSvg(xml)` drawn scaled with `DrawImage(image, x, y, width, height)` came out **blank** (tested 2026-10-11, rendering with `PluginApi.dll` outside the service): it reports a size of −1 × −1, even when the SVG sets `width` / `height`. `KeyRenderer.Logo` is the working example.

## Package Metadata (`src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml`)

The keys the manifest actually uses. The file's own comments explain each key.

```yaml
type: plugin4
name: xkcdviewer                 # must match PluginShortName in the .csproj, and the plugin class is {name}Plugin
displayName: xkcd viewer
description: ...
pluginFileName: xkcdviewerPlugin.dll
version: 1.0.0
author: ...
copyright: ...
pluginFolderWin: bin
pluginFolderMac: bin            # set, but the plugin hasn't been built or run on macOS yet
supportedDevices:
    - LoupedeckCtFamily
pluginCapabilities:              # e.g. HasHapticMapping (see Haptics)
minimumLoupedeckVersion: 6.0
license: MIT
licenseUrl: https://opensource.org/licenses/MIT
supportPageUrl: https://github.com/mrnil/xkcd-viewer/issues
homePageUrl: https://github.com/mrnil/xkcd-viewer
```

Don't add keys from memory. Check the SDK docs' distribution/manifest pages first, and run `LogiPluginTool verify` afterwards.

## Common Mistakes to Avoid

- **Mixing display modes**: overriding both `GetCommandDisplayName` and `GetCommandImage`. Pick one.
- **Forgetting `IsWidget`** on an image-only command: the display name is then drawn over the image as a title (see the gotcha below).
- **Not cleaning up**: everything subscribed or started in `Load` must be undone in `Unload`.
- **Blocking an SDK callback**: no `Thread.Sleep`, `.Result` or `.Wait()` in `RunCommand` / `ApplyAdjustment` / render methods. Use `Task.Run`.
- **Missing validation**: guard `actionParameter` in parameterized commands.
- **Doing I/O in a render method**: `GetCommandImage` is called often. Render from state that's already loaded.

## Haptics

Source: the `csharp/haptics/*` pages of the SDK docs. New in Plugin API 6.2.1 (Logi Options+ 1.95).

**Only an MX Master 4 mouse can play haptics.** The SDK's supported-devices page says it's the only haptics-capable device. A plugin can't make a Loupedeck or MX Creative Console buzz. It raises an *event*, and Logi Options+ plays a *waveform* on a connected MX Master 4 if the user has one.

Mechanism (plugin events plus a mapping file; there's no haptic API as such):

1. Declare the capability: `pluginCapabilities: - HasHapticMapping` in `LoupedeckPackage.yaml`.
2. List the events in `src/xkcdviewerPlugin/package/events/DefaultEventSource.yaml` (`events:` entries with `name`, `displayName`, optional `description`). Names start with a Latin letter or `_`, continue with letters/digits/`_`, are case-sensitive, and must match exactly across the code and both YAML files.
3. Map events to waveforms in `src/xkcdviewerPlugin/package/events/extra/eventMapping.yaml`: `haptics:`, then per event a `DEFAULT: <waveform>` and optionally a device-specific key (`MX Master 4: <waveform>`). Always provide `DEFAULT`.
4. In code: `this.PluginEvents.AddEvent(name, displayName, description)` in `Plugin.Load()`, then `this.PluginEvents.RaiseEvent(name)`. Do both the YAML and the `AddEvent` call, as the SDK tutorial does.
5. Waveforms: `sharp_state_change`, `damp_state_change`, `sharp_collision`, `damp_collision`, `subtle_collision`, `happy_alert`, `angry_alert`, `completed`, `square`, `wave`, `firework`, `mad`, `knock`, `jingle`, `ringing`.
6. The docs' advice: subtle waveforms for frequent events, and don't trigger haptics too often.

A mismatched event name fails silently: the mouse just never buzzes. When haptics are added, write a test that reads the YAML files and checks the names against the constants in code.

**Checking without the mouse**: create an empty file named `enablelogs` (no extension) in the Logi Plugin Service data directory (next to `LoupedeckSettings.ini`). The service then writes `Logs\messages\eventSources` and `Logs\testing\event_sources.txt`, which record raised events. Logging slows the service down, so delete the file afterwards.

## Known Device Gotchas

None of these is in the SDK docs. The first two were found on real devices in a sibling plugin project; the rest on this plugin's own hardware tests.

### An image-only command needs `IsWidget`, even with parameters

A command that overrides `GetCommandImage` but leaves `IsWidget` unset still has its (parameter) display name drawn as a big title over the image. Overriding `GetCommandImage` alone doesn't suppress it. Fix: set `this.IsWidget = true`. The action list still shows the display name, and this was confirmed on a Loupedeck Live S and an MX Keypad. Don't "fix" it by returning an empty `GetCommandDisplayName` (that's the mixing-display-modes mistake).

### MX Keypad: `ActionEditorCommand` images never reach the device

An `ActionEditorCommand` with `IsWidget = true` and a `GetCommandImage` override draws correctly on a Loupedeck CT. On an MX Keypad, the key only ever shows the static `DisplayName`, even though `GetCommandImage` is invoked with a sensible size, returns a valid bitmap and throws nothing, and `ActionImageChanged()` is called. `PluginDynamicCommand` / `PluginDynamicFolder` widget images **do** render on MX Keypad, so the gap is specific to `ActionEditorCommand`'s image path.

**Consequence**: build any action that needs a custom image on `PluginDynamicCommand` (or a folder), not `ActionEditorCommand`. Don't re-debug the rendering code if you hit this.

### Dynamic folders (confirmed on MX Creative Keypad, Live S and CT for this plugin)

- **Folder key images get a caption.** The SDK shrinks the image and draws the display name under it. Folders have no `IsWidget`: return `String.Empty` from `GetCommandDisplayName` and the image fills the key.
- **The SDK Back key can't be removed.** With `ButtonArea` it takes grid slot 0 on all three devices. With `None` it stays in slot 0 on the keypad and Live S, and moves to the side strip on the CT. `Navigation` is obsolete: override `GetNavigationArea` instead.
- **Names fill left to right, top to bottom, at the device's real width**, after the Back key. Any extra spill onto an automatic second page.
- **The device is known**: `GetButtonPressActionNames(DeviceType)` receives the real device. See the `DeviceType` table in `product.md`.
- **`CommandImageChanged` per key redraws a whole block at once**, with no visible tearing.

### A user-set caption overrides the dynamic display name

Once a user types a caption for a button in the Loupedeck/Options+ UI, that text permanently replaces the value from `GetCommandDisplayName`, and there's no known way to reset it. Anything the user must always see (state, the comic number) should be drawn into the image, not left only in the display name.

## Testing Checklist (on device)

- [ ] Each action appears in the action list under the right group
- [ ] Parameterized commands ignore a null/empty parameter
- [ ] The display updates after a state change
- [ ] Embedded resources load
- [ ] Offline / API failure shows a sensible placeholder, not a blank or frozen button
- [ ] Unload/reload (rebuild while running) leaves no duplicate timers or handlers
- [ ] Tried on each device in `supportedDevices`
