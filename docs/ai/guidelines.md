# Development Guidelines

Every example in this file follows the rules it describes. Keep it that way when editing.

## Code Style (EditorConfig Enforced)

### Type Names: BCL Names, Not Keywords

```csharp
// CORRECT
String name;
Boolean isActive;
Int32 count;

// WRONG
string name;
bool isActive;
int count;
```

The build does **not** flag this one (see `tech.md`), so check it in review.

### `this.` Qualification: Always Required

```csharp
// CORRECT
this._client.GetComicAsync(number)
this._log.Warning("message")
this.ActionImageChanged()

// WRONG
_client.GetComicAsync(number)
ActionImageChanged()
```

Static members are qualified with the type name: `PluginLog.Info(...)`, `xkcdviewerPlugin.Instance`.

### Private Fields: Underscore Prefix, No Redundant Initializers

```csharp
private readonly IxkcdClient _client;
private readonly IPluginLog _log;
private String _title = String.Empty;
private Int32 _currentNumber;          // not "= 0" - that is already the default
```

### `var`: Preferred

```csharp
var comic = await this._client.GetComicAsync(number);
var isLatest = comic.Num == this._latestNumber;
```

Use an explicit type only where `var` would hide it (e.g. an ambiguous numeric literal).

### Braces Always Required

```csharp
// CORRECT
if (comic == null)
{
    return;
}

// WRONG
if (comic == null)
    return;
```

### `using` Directives Inside Namespace

```csharp
namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Net.Http;
}
```

### Allman Brace Style

Opening brace always on its own line, for types, members and blocks alike.

### The xkcd Name: Always Lowercase

"xkcd" is always written in lowercase: in user-facing text, file names, package names **and code identifiers**. This is a deliberate exception to PascalCase. The plugin's own name, **xkcdviewer**, is one all-lowercase word too (`xkcdviewerPlugin`, `Loupedeck.xkcdviewerPlugin`), while types about the xkcd website keep plain "xkcd" (`IxkcdClient`, `xkcdComic`):

```csharp
// CORRECT
namespace Loupedeck.xkcdviewerPlugin
public class xkcdviewerPlugin : Plugin
public interface IxkcdClient
public class xkcdComic
var client = new xkcdClient(httpClient);

// WRONG
public class XkcdPlugin : Plugin
public interface IXkcdClient
```

Only the "xkcd" part stays lowercase; the rest of the identifier follows the normal rules (`IxkcdClient`, `_xkcdClient`, `LoadxkcdComic` is fine but prefer `LoadComic`). `.editorconfig`'s PascalCase naming rules are `suggestion` severity, so these names don't produce build warnings. Don't raise those rules to `warning` without first adding an exception for this.

### Comments

Explain *why*, not *what*. Don't write comments that repeat the next line ("// Increase the counter") or restate the SDK's template text.

---

## Service Layer Patterns

Services wrap anything stateful or I/O-bound (the xkcd HTTP API, a cache) behind an interface, with no SDK types, so they can be tested.

### Async Work Off the SDK Thread

SDK callbacks (`RunCommand`, `ApplyAdjustment`) must return quickly. Do network work in `Task.Run` and catch everything inside, because an unobserved exception there is lost:

```csharp
public void RefreshLatest()
{
    Task.Run(async () =>
    {
        try
        {
            var comic = await this._client.GetLatestAsync();
            this.OnComicLoaded(comic);
        }
        catch (Exception ex)
        {
            this._log.Error($"Failed to load the latest comic: {ex.Message}");
        }
    });
}
```

### Safe Default Return Pattern (Query Methods)

Query methods return safe defaults on failure; they never throw into the SDK:

```csharp
public async Task<xkcdComic> GetComicAsync(Int32 number)
{
    if (number < 1)
    {
        this._log.Warning($"Cannot get comic {number} - comic numbers start at 1");
        return null;
    }

    try
    {
        return await this._client.GetComicAsync(number);
    }
    catch (Exception ex)
    {
        this._log.Error($"Failed to get comic {number}: {ex.Message}");
        return null;
    }
}
```

Safe defaults by type:

- `Boolean` → `false`
- `String` → `String.Empty` or a domain-specific placeholder
- Arrays/collections → empty (`Array.Empty<T>()`)
- Model objects → `null` (callers must check)

### Input Validation Before External Calls

Validate parameters (range, null/empty) before any I/O, log a `Warning` and return early. See `secure-coding.md`.

### State Guard Pattern

Check current state before operations that need a specific state, e.g. ignore a "next comic" press while a fetch is already in flight rather than queuing a second one.

---

## Command (Action) Layer Patterns

### Constructor Shape

Pass display name, description and group to the base constructor. Keep the constructor to cheap wiring with no I/O:

```csharp
public class LatestComicCommand : PluginDynamicCommand
{
    public LatestComicCommand()
        : base(displayName: "Latest Comic", description: "Shows the latest xkcd", groupName: "Comics")
    {
    }
}
```

When commands need events from the plugin (see "Notification Interface Pattern" below), they also self-register: `xkcdviewerPlugin.Instance?.RegisterCommand(this);`. Add a static `Instance` to a command only when another class actually needs to call it.

### Null Guard in RunCommand

Parameterized commands must guard `actionParameter`:

```csharp
protected override void RunCommand(String actionParameter)
{
    if (String.IsNullOrEmpty(actionParameter))
    {
        return;
    }

    xkcdviewerPlugin.Instance?.OpenComic(actionParameter);
}
```

### Render Methods Read State; They Never Fetch

`GetCommandImage` / `GetCommandDisplayName` / `GetAdjustmentValue` are synchronous and called often. Read the state the plugin/service already holds and return a placeholder while nothing is loaded. See `structure.md` pattern 4.

```csharp
protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
{
    var comic = xkcdviewerPlugin.Instance?.CurrentComic;
    return comic == null
        ? ButtonTextRenderer.RenderText("xkcd\nloading…", imageSize)
        : ButtonTextRenderer.RenderText(comic.Title, imageSize);
}
```

### Targeted Refresh

Refresh only what changed: `ActionImageChanged(actionParameter)` for one parameter, `ActionImageChanged()` for a parameterless command, `AdjustmentValueChanged()` for a dial. Don't rebuild parameter lists (`ParametersChanged()`) just to redraw one image. See `icon-update-patterns.md`.

### Shared Base Classes

Introduce a base class (e.g. `ToggleCommandBase`) when the **second** command of the same shape appears, not before.

### ActionEditorCommand: For User-Configured Actions

Use constants for control names and validate required parameters. Read `adjustable-command-pattern.md` first, including the MX Keypad image caveat.

---

## Image Rendering

See `image-rendering-simplified.md`: small static helpers, not a factory/store abstraction.

### Colour Conventions

- Green (`BitmapColor.Green`): active, healthy
- Red (`BitmapColor.Red`): error
- Yellow (`new BitmapColor(255, 200, 0)`): warning, loading
- Grey (`new BitmapColor(128, 128, 128)`): idle, offline

Colour is never the only signal: pair it with text or an icon shape so colour-blind users get the same information.

---

## Notification Interface Pattern

Introduce this when more than one command must react to the same plugin event. Commands declare which events they care about by implementing interfaces:

```csharp
public interface IComicAwareCommand
{
    void OnComicChanged();
}

public class LatestComicCommand : PluginDynamicCommand, IComicAwareCommand
{
    public void OnComicChanged() => this.ActionImageChanged();
}
```

### Adding a New Notification Type

1. Add the interface (e.g. `src/Services/Notifications.cs`)
2. Add `NotifyXxx()` to the command coordinator using `this.NotifyEach<TInterface>(nameof(TInterface.OnXxx), c => c.OnXxx(...))`, with each command's callback isolated in its own try/catch
3. Call it from the plugin class where the event originates
4. Implement the interface in the relevant commands

---

## Testing Patterns

See `test-coverage.md` for policy, targets and patterns. Test class shape:

```csharp
public class ComicServiceTests
{
    private readonly Mock<IxkcdClient> _mockClient;
    private readonly Mock<IPluginLog> _mockLog;
    private readonly ComicService _service;

    public ComicServiceTests()
    {
        this._mockClient = new Mock<IxkcdClient>();
        this._mockLog = new Mock<IPluginLog>();
        this._service = new ComicService(this._mockClient.Object, this._mockLog.Object);
    }
}
```

Error log messages must contain both the entity (e.g. the comic number) and the exception message, and tests must verify that.

Services depend on an injectable `IPluginLog` (a thin adapter over the static `PluginLog`), not on `PluginLog` directly, so tests can verify what gets logged.

---

## Logging Conventions

### Log Levels (the `PluginLog` API)

- `Verbose`: high-frequency detail (render calls, intermediate steps)
- `Info`: significant state changes (comic loaded, plugin loaded/unloaded)
- `Warning`: guard-clause violations (invalid input, offline, invalid state)
- `Error`: caught exceptions, with context

Each level has an `(Exception ex, String text)` overload. Use it when the stack trace matters.

### Log Message Format

Name the entity and the action:

```csharp
this._log.Info($"Loaded comic {comic.Num}");
this._log.Warning($"Cannot load comic {number} - out of range");
this._log.Error($"Failed to load comic {number}: {ex.Message}");
```

Never log credentials or tokens. Sanitize file paths (they contain the user name).

---

## Timing Constants

Centralise delays, timeouts, refresh intervals and cache lifetimes in one static class (`PluginTimings`). Never hardcode them inline. Anything that waits should take its durations as constructor parameters so tests can shrink them.

---

## Adding a New Feature: Checklist

### New Service Call

1. Add the method to the service interface
2. Implement it in the concrete class (validation → I/O → safe default on failure)
3. Expose it from the plugin class if actions need it
4. Test all three paths: success, unavailable/invalid input, exception

### New Command

1. Create the class in `src/Actions/`
2. Inherit from the right SDK base (see `sdk-quick-reference.md`)
3. Pass display name, description and group to the base constructor
4. Guard `actionParameter` in `RunCommand` if the command is parameterized
5. Render from already-loaded state only
6. Pull any pure logic out into a tested helper (`structure.md` pattern 5)
7. Embed icons under `src/Resources/Icons/` as `<EmbeddedResource>` in the `.csproj`
8. Add a constructor smoke test
9. Update `structure.md`'s layout tree
