# Project Structure

## Current Repository Layout

```text
xkcdPlugin/
├── src/                              # Plugin source code
│   ├── Actions/                      # Loupedeck SDK command/adjustment/folder classes
│   │   ├── CounterCommand.cs         # SDK template example - delete with the first real action
│   │   └── CounterAdjustment.cs      # SDK template example - delete with the first real action
│   ├── Helpers/
│   │   ├── ComicFit.cs               # Comic image → letterboxed picture or title card, and where it goes
│   │   ├── ComicNumbers.cs           # Prev/Next comic numbers (stays within 1..latest)
│   │   ├── DeviceGrid.cs             # DeviceType → grid columns, as observed on hardware; 3 for anything else
│   │   ├── FolderLayout.cs           # The folder's keys (Prev, Next, padding, comic tiles) and their action names
│   │   ├── PluginLog.cs              # SDK-provided static logging facade
│   │   ├── PluginLogAdapter.cs       # IPluginLog over PluginLog, for injecting into services
│   │   ├── PluginResources.cs        # Embedded resource access helper (filename-only lookup)
│   │   └── PluginTimings.cs          # Timeouts and intervals (HTTP timeout so far)
│   ├── Models/
│   │   └── xkcdComic.cs              # A validated comic: number, display-ready title, image URL or null, page URL
│   ├── Services/
│   │   ├── ComicJson.cs              # info.0.json → xkcdComic; normalizes the title, drops disallowed images
│   │   ├── IPluginLog.cs             # Logging interface services depend on
│   │   ├── IxkcdClient.cs            # The xkcd API: latest, by number, image bytes. Null on any failure
│   │   ├── xkcdClient.cs             # HttpClient implementation: no redirects, timeout, size caps
│   │   └── xkcdUrls.cs               # Every URL requested or opened is built or checked here
│   ├── package/metadata/             # LoupedeckPackage.yaml (manifest) + Icon256x256.png
│   ├── xkcdPlugin.cs                 # Main plugin class (Plugin subclass)
│   ├── xkcdApplication.cs            # ClientApplication subclass (unused: this is a universal plugin)
│   ├── xkcdPlugin.csproj
│   ├── xkcdPlugin.csproj.user        # Visual Studio F5 target (LogiPluginService.exe); git-ignored
│   └── Directory.Build.props         # LangVersion + obj/ location - see note below
├── tests/
│   └── xkcdPlugin.Tests/             # xUnit + Moq; mirrors src/ folders (Helpers/ → Helpers/*Tests.cs)
│       ├── Fixtures/                 # Real info.0.json responses captured from the API (see its README.md)
│       └── StubHttpMessageHandler.cs # Fake network for xkcdClient tests
├── docs/ai/                          # AI coding rules and SDK reference (this directory)
├── .vscode/                          # Attach-to-LogiPluginService debug config + build/pack tasks
├── .editorconfig                     # Enforces the code style in docs/ai/guidelines.md, for src/ and tests/
├── xkcdPlugin.sln
├── AGENTS.md / CLAUDE.md
└── .gitignore
```

**Naming**: "xkcd" is always lowercase, including in code (see `guidelines.md` → "The xkcd Name"). The folder, project, assembly, `.sln` and plugin class are `xkcdPlugin`, the namespace is `Loupedeck.xkcdPlugin`, and the package name (`LoupedeckPackage.yaml` `name:`, `.csproj` `PluginShortName`) is `xkcd`. Those two package names must match: the post-build reload URL (`loupedeck:plugin/xkcd/reload`) and `logiplugintool uninstall xkcd` both use them.

**`src/Directory.Build.props` gotcha**: `BaseIntermediateOutputPath` is computed from `$(MSBuildThisFileDirectory)`, **never** `$(SolutionDir)`. `SolutionDir` is only defined when building through the `.sln`; building the bare `.csproj` leaves it empty, which sends `obj/` to a different folder and leaves stray generated files. The next `.sln` build then fails with `CS0579` (duplicate assembly attributes). `BaseOutputPath` lives only in the `.csproj`.

## Target Layer Breakdown (grow into this as features land)

```text
src/
├── Actions/     # SDK glue only: commands, adjustments, folders. Exempt from coverage targets.
├── Services/    # Business logic + the xkcd HTTP client behind an interface. No SDK types. Tested.
├── Models/      # Plain data (e.g. the comic DTO). No SDK types.
├── Helpers/     # Static helpers. Pure ones are fully tested; SDK-bound renderers are thin.
└── Resources/   # Embedded assets (icons/*.svg), one <EmbeddedResource> entry each in the .csproj.
```

Create a folder when its first file arrives, not ahead of time. Once one feature area needs several files, give it a same-named subfolder with a matching namespace suffix (`Services.Comics`), rather than filling a flat folder with unrelated files.

**Changing an action's namespace or class name can orphan buttons users have already placed**, because the Logi software stores placed actions by identity. Decide on action names and namespaces before users start placing them, and test any later rename on a device.

### `src/Actions/` (SDK-dependent)

Group commands by SDK base class: `PluginDynamicCommand` (buttons), `PluginDynamicAdjustment` (dials), `PluginDynamicFolder` (dynamic lists), `ActionEditorCommand` (user-configured actions; see `adjustable-command-pattern.md`). Introduce a shared base (e.g. `ToggleCommandBase`) only when a second command of the same shape appears.

### `src/Helpers/`

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
        ↓ via xkcdPlugin.Instance
xkcdPlugin (constructs and owns the services; thin orchestration)
        ↓
Services (IxkcdClient → HttpClient → https://xkcd.com)
```

Dependencies point one way: Actions → Plugin → Services. Services never reference SDK types; that's what makes them testable.

### 2. Singleton Instance

`xkcdPlugin` doesn't have one yet. Add `public static xkcdPlugin Instance { get; private set; }`, set in the constructor, once the first action needs a service. Actions then reach services through `xkcdPlugin.Instance?.…`. Always null-propagate: actions can be constructed before the plugin is fully initialized.

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
├── Debug/metadata/   ← copied from src/package/
├── Release/bin/
└── Release/metadata/
obj/                  ← intermediate files (repo root, see Directory.Build.props)
```

Post-build writes `%LocalAppData%\Logi\LogiPluginService\Plugins\xkcdPlugin.link` (pointing at `bin/<Configuration>/`) and triggers `loupedeck:plugin/xkcd/reload`. `dotnet clean` deletes the link.

## Package Format

`.lplug4`: created with `LogiPluginTool pack`, checked with `LogiPluginTool verify`. The manifest is `src/package/metadata/LoupedeckPackage.yaml`.
