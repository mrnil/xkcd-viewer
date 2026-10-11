# Project Structure

## Current Repository Layout

```text
xkcdviewerPlugin/
├── src/
│   └── xkcdviewerPlugin/             # The plugin project
│       ├── Actions/                  # Loupedeck SDK command/adjustment/folder classes
│       │   └── ComicFolder.cs        # The xkcd folder: layout, key images, presses. Users place it by this name
│       ├── Helpers/
│       │   ├── BrowserLauncher.cs    # Opens a comic page (only https://xkcd.com/{num}/) in the default browser
│       │   ├── ComicFit.cs           # Comic image → letterboxed picture or title card, and where it goes
│       │   ├── ComicNumbers.cs       # Prev/Next comic numbers (stays within 1..latest)
│       │   ├── DeviceGrid.cs         # DeviceType → grid columns, as observed on hardware; 3 for anything else
│       │   ├── FolderLayout.cs       # The folder's keys (Prev, Next, padding, comic tiles) and their action names
│       │   ├── KeyContent.cs         # What each key shows, decided from the ComicView
│       │   ├── KeyRenderer.cs        # Draws keys with BitmapBuilder (SDK-bound, thin; decisions live in tested helpers)
│       │   ├── LruCache.cs           # Generic count-limited LRU; the folder caches comic areas in it
│       │   ├── PluginLog.cs          # SDK-provided static logging facade
│       │   ├── PluginLogAdapter.cs   # IPluginLog over PluginLog, for injecting into services
│       │   ├── PluginResources.cs    # Embedded resource access helper (filename-only lookup)
│       │   ├── PluginTimings.cs      # Timeouts and intervals: HTTP timeout, failed-load retry delay
│       │   └── TitleCardLayout.cs    # Splits "#num" and the title across the six tiles, no word crossing a seam
│       ├── Models/
│       │   ├── ComicView.cs          # What the folder draws: current comic, Loading/Ready/Failed, image, Prev/Next possible
│       │   └── xkcdComic.cs          # A validated comic: number, display-ready title, image URL or null, page URL
│       ├── Services/
│       │   ├── ComicJson.cs          # info.0.json → xkcdComic; normalizes the title, drops disallowed images
│       │   ├── ComicService.cs       # Current comic, caches, preloads ±2, retry delay; raises Changed
│       │   ├── ImageCache.cs         # LRU of image bytes, limited by count and total size
│       │   ├── IPluginLog.cs         # Logging interface services depend on
│       │   ├── IxkcdClient.cs        # The xkcd API: latest, by number, image bytes. Null on any failure
│       │   ├── xkcdClient.cs         # HttpClient implementation: no redirects, timeout, size caps
│       │   └── xkcdUrls.cs           # Every URL requested or opened is built or checked here
│       ├── package/metadata/         # LoupedeckPackage.yaml (manifest) + Icon256x256.png (generated, see art/)
│       ├── Resources/Images/         # FolderButton116.png / FolderButton80.png: the folder key's logo (generated, see art/)
│       ├── xkcdviewerPlugin.cs       # Main plugin class (Plugin subclass)
│       ├── xkcdviewerApplication.cs  # ClientApplication subclass (unused: this is a universal plugin)
│       ├── xkcdviewerPlugin.csproj
│       └── xkcdviewerPlugin.csproj.user # Visual Studio F5 target (LogiPluginService.exe); git-ignored
├── tests/
│   └── xkcdviewerPlugin.Tests/       # The test project. xUnit + Moq; mirrors the plugin's folders (Helpers/ → Helpers/*Tests.cs)
│       ├── Actions/                  # Constructor smoke tests only (SDK glue is exempt)
│       ├── Fixtures/                 # Real info.0.json responses captured from the API (see its README.md)
│       ├── ManualTimeProvider.cs     # A clock tests move by hand
│       ├── StubHttpMessageHandler.cs # Fake network for xkcdClient tests
│       └── TestWait.cs               # Bounded wait for a background condition, instead of sleeping
├── art/
│   ├── xkcdviewer-logo.jpg           # Source logo: the owner's own pencil drawing (1024x1024 JPG, on paper)
│   └── make-icons.ps1                # Regenerates the package icon and folder key images from it; edit this, not the PNGs
├── docs/ai/                          # AI coding rules and SDK reference (this directory)
├── .vscode/                          # Attach-to-LogiPluginService debug config + build/pack tasks
├── .editorconfig                     # Enforces the code style in docs/ai/guidelines.md, for src/ and tests/
├── Directory.Build.props             # Settings shared by both projects: TargetFramework, LangVersion, code style, PluginApi reference
├── xkcdviewerPlugin.sln
├── AGENTS.md / CLAUDE.md
└── .gitignore
```

**Naming**: "xkcd" is always lowercase, including in code (see `guidelines.md` → "The xkcd Name"). The plugin itself is called **xkcdviewer**, not plain xkcd (decided 2026-10-11, for licensing: it's a viewer of xkcd, not xkcd). The repo folder, project, assembly, `.sln` and plugin class are `xkcdviewerPlugin`, the namespace is `Loupedeck.xkcdviewerPlugin`, and the package name and display name (`LoupedeckPackage.yaml` `name:` / `displayName:`, `.csproj` `PluginShortName`) are `xkcdviewer`. The plugin service names its log (`plugin_logs/xkcdviewer.log`) and settings after the package name. The SDK requires the plugin class to be `{name}Plugin`, and the two package names must match: the post-build reload URL (`loupedeck:plugin/xkcdviewer/reload`) and `logiplugintool uninstall xkcdviewer` both use them. Types named after the xkcd website (`xkcdClient`, `xkcdComic`, `xkcdUrls`) keep plain "xkcd": they describe the site, not the plugin.

**Project layout**: each project lives in its own folder (`src/xkcdviewerPlugin/`, `tests/xkcdviewerPlugin.Tests/`) with one `.sln` at the repo root. Settings both projects need go in the root `Directory.Build.props`; settings for one project stay in its `.csproj`. Each project keeps its `obj/` in its own folder (the default), so don't set `BaseIntermediateOutputPath`: two projects sharing an `obj/` overwrite each other's `project.assets.json`. The plugin's `BaseOutputPath` alone points at the repo-root `bin/`, because the `.link` file and `LogiPluginTool pack` expect the output there.

## Target Layer Breakdown (grow into this as features land)

```text
src/xkcdviewerPlugin/
├── Actions/                          # SDK glue only: commands, adjustments, folders. Exempt from coverage targets.
├── Services/                         # Business logic + the xkcd HTTP client behind an interface. No SDK types. Tested.
├── Models/                           # Plain data (e.g. the comic DTO). No SDK types.
├── Helpers/                          # Static helpers. Pure ones are fully tested; SDK-bound renderers are thin.
└── Resources/                        # Embedded assets (icons/*.svg), one <EmbeddedResource> entry each in the .csproj.
```

Create a folder when its first file arrives, not ahead of time. Once one feature area needs several files, give it a same-named subfolder with a matching namespace suffix (`Services.Comics`), rather than filling a flat folder with unrelated files.

**Changing an action's namespace or class name can orphan buttons users have already placed**, because the Logi software stores placed actions by identity. Decide on action names and namespaces before users start placing them, and test any later rename on a device.

### `src/xkcdviewerPlugin/Actions/` (SDK-dependent)

Group commands by SDK base class: `PluginDynamicCommand` (buttons), `PluginDynamicAdjustment` (dials), `PluginDynamicFolder` (dynamic lists), `ActionEditorCommand` (user-configured actions; see `adjustable-command-pattern.md`). Introduce a shared base (e.g. `ToggleCommandBase`) only when a second command of the same shape appears.

### `src/xkcdviewerPlugin/Helpers/`

| File | Purpose |
|------|---------|
| `PluginLog.cs` | Static logging facade over the SDK's `PluginLogFile` (exists) |
| `PluginResources.cs` | Embedded resource access (exists) |
| `ButtonImageHelper.cs` / `ButtonTextRenderer.cs` | Button image drawing: create when first needed, see `image-rendering-simplified.md` |
| `PluginTimings.cs` | Centralised timing constants: create when the first delay/timeout appears |

## Architectural Patterns

### 1. Layered Architecture

```text
Loupedeck SDK
  └── Actions layer (commands / adjustments / folders)
        ↓ via xkcdviewerPlugin.Instance
xkcdviewerPlugin (constructs and owns the services; thin orchestration)
        ↓
Services (IxkcdClient → HttpClient → https://xkcd.com)
```

Dependencies point one way: Actions → Plugin → Services. Services never reference SDK types; that's what makes them testable.

### 2. Singleton Instance

`xkcdviewerPlugin.Instance` is set in the plugin's constructor; the services (`Comics`, `ServiceLog`) are created in `Load()`. Actions reach them through `xkcdviewerPlugin.Instance?.Comics`. Always null-propagate: actions can be constructed before the plugin is fully initialized.

### 3. Command Registry / Self-Registration (introduce when needed)

Once more than one command must react to the same event (e.g. "new comic available"), commands register themselves in their constructor and a coordinator dispatches by interface, with per-command exception isolation:

```csharp
this.NotifyEach<IComicAwareCommand>(nameof(IComicAwareCommand.OnComicChanged), c => c.OnComicChanged(comic));
```

Add notification interfaces as real needs appear; don't design the hierarchy up front. See `refactoring-patterns.md` and `guidelines.md` → "Notification Interface Pattern".

### 4. Async Result → Synchronous Render

`GetCommandDisplayName`/`GetCommandImage`/`GetAdjustmentValue` must return synchronously, while the data they show (a comic) comes from a network call. Pattern: fetch in `Task.Run`, store the result in a field, then call `ActionImageChanged()` / `AdjustmentValueChanged()`. The render method only reads the stored value and returns a placeholder while it's empty. It never blocks and never starts a fetch per render call. If one key can start a fetch, keep an in-flight set so repeated renders don't pile up duplicate fetches.

### 5. Extracting Pure Logic Out of SDK-Exempt Code (apply proactively)

The `Actions/` exemption covers SDK glue (`GetCommandImage`, `RunCommand`, `AddParameter`), not every line in the class. When a method mixes "get data" (async, SDK-touching) with "decide what to do with it" (synchronous, pure), split at that seam. Move the pure half into a static method with no SDK types in its signature, and test it fully. Typical candidates here: wrapping a comic title onto button lines, clamping a comic number to `1..latest`, choosing a random comic.

## Build Output

```text
bin/
├── Debug/bin/        ← DLL + dependencies (hot-reload via .link file)
├── Debug/metadata/   ← copied from src/xkcdviewerPlugin/package/
├── Release/bin/
└── Release/metadata/
src/xkcdviewerPlugin/obj/   ← intermediate files (each project keeps its own obj/)
```

Post-build writes `%LocalAppData%\Logi\LogiPluginService\Plugins\xkcdviewerPlugin.link` (pointing at `bin/<Configuration>/`) and triggers `loupedeck:plugin/xkcdviewer/reload`. `dotnet clean` deletes the link.

## Package Format

`.lplug4`: created with `LogiPluginTool pack`, checked with `LogiPluginTool verify`. The manifest is `src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml`.
