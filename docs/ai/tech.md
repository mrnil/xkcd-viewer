# Technology Stack

## Runtime and Language

| Item | Value |
| --- | --- |
| Language | C# (`LangVersion` latest) |
| Target Framework | .NET 10.0 (`net10.0`). The installed `PluginApi.dll` (6.4.1.3246+) is built against .NET 10 and a net8.0 project fails to resolve it (`CS1705`) |
| Nullable Reference Types | Disabled |
| Implicit Usings | Enabled |
| Root Namespace | `Loupedeck.xkcdviewerPlugin` |
| Plugin type | Universal (`HasNoApplication => true`, `UsesApplicationApiOnly => true`) |

## Key Dependencies

### Plugin Project (`src/xkcdviewerPlugin/xkcdviewerPlugin.csproj`)

| Package | Version | Purpose |
| --- | --- | --- |
| `PluginApi.dll` | runtime-provided (6.4.1.3246 at time of writing) | Logi Actions SDK: `Plugin`, `PluginDynamicCommand`, `PluginDynamicAdjustment`, `PluginDynamicFolder`, `ActionEditorCommand`, `BitmapBuilder`, etc. |

No NuGet packages yet. `System.Net.Http` and `System.Text.Json` ship with .NET 10 and are enough for xkcd's JSON API, so don't add a third-party HTTP or JSON library without a reason. Keep this table current.

### Test Project (`tests/xkcdviewerPlugin.Tests/xkcdviewerPlugin.Tests.csproj`)

| Package | Version | Purpose |
| --- | --- | --- |
| `xunit` | 2.9.3 | Test framework |
| `xunit.runner.visualstudio` | 3.1.4 | Test adapter for `dotnet test` and IDEs |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test host |
| `Moq` | 4.20.72 | Mocks (`IxkcdClient`, `IPluginLog`) |
| `coverlet.collector` | 6.0.4 | `dotnet test --collect:"XPlat Code Coverage"` |

The `PluginApi` reference, `PluginApiDir`, `TargetFramework`, `LangVersion` and `EnforceCodeStyleInBuild` are set once, in the repo-root `Directory.Build.props`, so both projects get them. The test project needs the `PluginApi` reference too: a plain file `<Reference>` doesn't flow through a `ProjectReference` at compile time, so without it the build breaks as soon as test code touches a `PluginApi` type (e.g. `DeviceType`).

## SDK Plugin API Location

- **Windows**: `C:\Program Files\Logi\LogiPluginService\PluginApi.dll`
- **macOS**: `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll`

The root `Directory.Build.props` resolves this through `PluginApiDir`. A Logi Options+ update can bump the `PluginApi` version and its target framework: if the build suddenly fails with `CS1705`, that is why.

## Code Style: EditorConfig Enforced Rules

`.editorconfig`, at the repo root so it covers both `src/` and `tests/`, sets these rules (as warnings):

- **`this.` qualification required** for fields, methods, properties and events
- **`var` preferred**: `csharp_style_var_for_built_in_types` and `csharp_style_var_when_type_is_apparent` are `warning`; `csharp_style_var_elsewhere` is `silent`
- **BCL type names, not keywords**: `String`, `Int32`, `Boolean`, `Single`, not `string`, `int`, `bool`, `float`
- **Braces always required** (`csharp_prefer_braces = true`)
- **`using` directives inside the namespace**
- **Allman braces**: opening brace on its own line
- **4-space indentation**, CRLF line endings
- **Readonly fields** and **null propagation** preferred
- **Private fields**: `_camelCase`
- **Interfaces**: `I` prefix; **types, methods, properties**: PascalCase

See `guidelines.md` for examples.

**How it is enforced.** `EnforceCodeStyleInBuild` is on in both `.csproj` files, so `dotnet build` reports the rules as warnings. The build has **zero warnings**; treat any new one as a defect to fix, not noise. They stay warnings, not errors, so a style slip never blocks the hot-reload build. The build does **not** check:

- **BCL type names**: `int` and `string` aren't flagged even with an explicit `IDE0049` severity. Check these yourself and in review.
- **`_camelCase` private fields (`SX1309`)**: the project references no analyzer that implements it.
- **CRLF line endings**: nothing checks them.

When a rule would make the code worse, first try to restructure so the code satisfies it and still reads well. If that fails, suppress that one diagnostic at that one place, with a comment saying why. Never lower a rule's severity project-wide to silence a warning.

## Build Commands

```bash
# Development build (hot-reloads into Logi Plugin Service via the .link file)
dotnet build xkcdviewerPlugin.sln

# Release build (for packaging)
dotnet build xkcdviewerPlugin.sln -c Release

# Clean (also deletes the .link file)
dotnet clean xkcdviewerPlugin.sln
```

VS Code: the `.vscode/tasks.json` tasks run the same commands, and the "Attach to Logi Plugin Service" launch config debugs the running plugin. `.vscode/settings.json` sets `dotnet.defaultSolution` so C# Dev Kit always opens `xkcdviewerPlugin.sln`. Without it, Dev Kit keeps loading whichever solution it opened last, which broke after the `xkcdPlugin` → `xkcdviewerPlugin` rename (`MSB1009`/`MSB4025` for the old paths). If the solution is ever renamed, update this setting too.

## Tests

```bash
dotnet test
```

See `test-coverage.md`.

## Packaging and Release

```bash
LogiPluginTool pack ./bin/Release ./xkcdviewer-v{VERSION}.lplug4
LogiPluginTool verify ./xkcdviewer-v{VERSION}.lplug4
LogiPluginTool metadata ./xkcdviewer-v{VERSION}.lplug4
```

`bin/Release/bin` should hold only `xkcdviewerPlugin.dll`, its `.deps.json` and `.pdb` (the package is about 64 KB). Logi Plugin Service supplies `PluginApi.dll` and its dependencies (SkiaSharp, Svg.Skia, Newtonsoft.Json, …), so `Directory.Build.props` marks the reference `Private=false` for the plugin project. If those DLLs show up in the output again, that setting has been lost.

**LogiPluginTool locations on this machine:**

- Preferred: `C:\Users\steph\.dotnet\tools\logiplugintool.exe` (dotnet global tool, callable as `LogiPluginTool`)
- Alternative: `B:\development\LogitechBeta\LogiPluginSdkTools\LogiPluginTool.exe`
- ❌ Don't use `C:\Program Files\Logi\LogiPluginService\LogiPluginTool.exe`: it's broken (missing companion DLL)

## Version Management

Update the version in both places for every release:

1. `src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml`: `version:`
2. `src/xkcdviewerPlugin/xkcdviewerPlugin.csproj`: `<Version>` (add it on the first release; it isn't there yet)

## CI/CD

None yet. If one is added: build validation plus dependency vulnerability scanning, with tests included once they're deterministic (see `test-coverage.md` → CI Note).
