# AGENTS.md

Instructions for AI coding assistants working in this repository (Claude Code, Cursor, Copilot, Codex, etc).

## What this project is

`xkcdviewerPlugin`: a Logitech/Loupedeck hardware plugin (MX Creative Console, Loupedeck CT/Live) that shows the current xkcd comic split across the device's keys, with navigation keys beside it. It's built on the Logi Actions SDK in C#/.NET 10. The primary target is the MX Creative Keypad; see `docs/ai/product.md`. The folder lays itself out per device from the `DeviceType` the SDK passes in (keypad 3×3, Live S 5×3, CT 4×3), and assumes **3×3 for any other device**. These and the other folder behaviours were confirmed on hardware; see "Hardware facts" in `product.md` before changing the layout.

The v1 folders are built (`src/xkcdviewerPlugin/Actions/`: `ComicFolder` is the Comic Browser, and `LatestComicFolder` / `RandomComicFolder` are it in another mode) and in hardware testing (they run on the MX Creative Keypad through the development `.link` load, and the 1.0.0 `.lplug4` installs correctly through Logi Options+ and Loupedeck on Windows; no published release yet). `docs/ai/product.md` → "Hardware test checklist" and "Still open" list what's left. There's also a **New Comic** command key (`NewComicCommand`) that lights up when a new comic is out. **The folder and command classes' names and namespace are what users' placed buttons are stored by**: don't rename them.

## Start here

Read these before making changes:

| File | Read this for |
| --- | --- |
| [`docs/ai/product.md`](docs/ai/product.md) | What the plugin does (a comic split across the keypad), target devices, layout, v1 scope, **risks to test on hardware first** |
| [`docs/ai/structure.md`](docs/ai/structure.md) | Repo layout, the layering to grow into, architectural patterns |
| [`docs/ai/tech.md`](docs/ai/tech.md) | Stack, what the build enforces, build/test/package commands |
| [`docs/ai/guidelines.md`](docs/ai/guidelines.md) | Code style, service/command layer patterns, testing patterns. **Read before writing code** |
| [`docs/ai/secure-coding.md`](docs/ai/secure-coding.md) | Security principles (input validation, network, files, memory) |
| [`docs/ai/test-coverage.md`](docs/ai/test-coverage.md) | Test architecture policy, coverage targets, why the SDK layer is exempt |
| [`docs/ai/commit-conventions.md`](docs/ai/commit-conventions.md) | Commit message rules |
| [`docs/ai/release-process.md`](docs/ai/release-process.md) | Full release checklist |

SDK reference material (read when the task touches that area):

- [`docs/ai/sdk-quick-reference.md`](docs/ai/sdk-quick-reference.md): command/adjustment/folder templates, lifecycle, haptics, **known device gotchas**
- [`docs/ai/image-rendering-simplified.md`](docs/ai/image-rendering-simplified.md): drawing button images
- [`docs/ai/icon-update-patterns.md`](docs/ai/icon-update-patterns.md): refreshing button images when state changes
- [`docs/ai/adjustable-command-pattern.md`](docs/ai/adjustable-command-pattern.md): `ActionEditorCommand` (user-configured actions)
- [`docs/ai/refactoring-patterns.md`](docs/ai/refactoring-patterns.md): when and how to split a growing plugin class

## When SDK knowledge is missing

The Logi Actions SDK (`PluginApi.dll`, the `Plugin`/`PluginDynamicCommand`/`PluginDynamicAdjustment`/`PluginDynamicFolder`/`ActionEditorCommand` base classes, etc.) has an AI-friendly documentation index at **<https://logitech.github.io/actions-sdk-docs/llms.txt>**. It links to the real docs pages: C# plugin development, haptics, icons, plugin features (logging, settings, localization, multistate actions), supported devices, distribution guidelines.

If the question isn't answered by the docs above, such as an API you haven't used before, a lifecycle or rendering edge case, or a method whose contract you're unsure of, **fetch and consult that index rather than guessing**. `PluginApi.dll` is a compiled, reference-only dependency (installed with Logi Plugin Service, not in this repo), so you can't read its source. Wrong guesses produce code that compiles but misbehaves on the device, and tests can't catch that because the `src/xkcdviewerPlugin/Actions/` layer is exempt from them.

## When xkcd API knowledge is missing

Comic data comes from xkcd's public JSON endpoints (`https://xkcd.com/info.0.json` for the latest comic, `https://xkcd.com/{num}/info.0.json` for a specific one). Fetch a real response and check field names and types before writing a model; don't write one from memory. Treat every field as untrusted input (see `docs/ai/secure-coding.md`).

## Non-negotiables

- Follow `docs/ai/guidelines.md` for code style. The repo-root `.editorconfig` requires BCL type names (`String`, not `string`), `var` where the type is apparent, `this.` qualification, `_camelCase` private fields and Allman braces. The build reports most of these (`EnforceCodeStyleInBuild`) and must stay at **zero warnings**. It does **not** check BCL type names or field naming, so check those yourself (see `docs/ai/tech.md`).
- "xkcd" is **always lowercase**, in prose, file names, package names and code identifiers alike (`xkcdviewerPlugin`, `IxkcdClient`), never `Xkcd` or `XKCD`. See `docs/ai/guidelines.md` → "The xkcd Name".
- All new business logic needs tests (see `docs/ai/test-coverage.md`). SDK glue in `src/xkcdviewerPlugin/Actions/` is exempt; the pure logic inside it is not.
- Commits follow Conventional Commits and explain **why**, not just what (see `docs/ai/commit-conventions.md`).
- Every `.md` file you write or edit uses consistent Markdown: a blank line before and after headings and fenced code blocks, no bare URLs, and ordered lists that actually increment.

## Keeping this current

Maintain `docs/ai/*.md` and the user-facing `README.md` like code; when a feature, device or build step changes, check the README too. When you change architecture, dependencies, versions or project layout, update the matching doc in the same commit. `structure.md`'s layout tree in particular must match the real `src/` tree.
