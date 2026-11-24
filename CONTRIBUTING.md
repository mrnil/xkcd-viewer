# Contributing to xkcd viewer

Thanks for your interest. Bug reports, device test results and pull requests are all welcome.

## Reporting a problem or an idea

Use the [issue templates](https://github.com/mrnil/xkcd-viewer/issues/new/choose). For a bug, say which device and software you're on and attach the plugin's log if you can. Report security problems privately instead: see [SECURITY.md](SECURITY.md).

Reports from devices other than the MX Creative Console keypad are especially useful. The Live S and CT layouts were checked once, and anything else falls back to the 3 × 3 layout.

## Setting up

You'll need:

- Windows (the plugin hasn't been built or run on macOS yet)
- [Logi Options+](https://www.logitech.com/software/logi-options-plus.html), which installs Logi Plugin Service and the `PluginApi.dll` the build references
- The .NET 10 SDK
- [gitleaks](https://github.com/gitleaks/gitleaks), for the pre-commit hook (`winget install Gitleaks.Gitleaks`)

Then, once per clone:

```bash
git config core.hooksPath .githooks   # commit message and secret checks
dotnet tool restore                   # LogiPluginTool, pinned in .config/dotnet-tools.json
dotnet build xkcdviewerPlugin.sln     # also links the plugin into Logi Plugin Service and reloads it
dotnet test
```

The **xkcd viewer** actions then appear in Logi Options+. `dotnet clean` removes the development link.

## Before you write code

Read [AGENTS.md](AGENTS.md) and the files it points to in [`docs/ai/`](docs/ai/). They're written for AI assistants but apply to everyone. In short:

- **Code style**: [`docs/ai/guidelines.md`](docs/ai/guidelines.md). The build checks most of it and must stay at zero warnings (CI treats warnings as errors). It doesn't check BCL type names (`String`, not `string`) or `_camelCase` private fields, so check those yourself.
- **"xkcd" is always lowercase**, in prose and in identifiers.
- **Tests first** for any business logic: write the failing test, make it pass, then tidy. SDK glue in `src/xkcdviewerPlugin/Actions/` is exempt, but the logic inside it isn't. See [`docs/ai/test-coverage.md`](docs/ai/test-coverage.md).
- **Security**: everything from xkcd is untrusted input, and only `xkcd.com` and `imgs.xkcd.com` are ever requested or opened. See [`docs/ai/secure-coding.md`](docs/ai/secure-coding.md).
- **Don't rename** the folder and command classes or their namespace: users' placed buttons are stored by those names.
- **Ask first** (open an issue) before adding a dependency, a new network call, or anything stored on disk.
- **Check layout changes on hardware.** Tests can't catch how the device draws or pages keys; see "Hardware facts" in [`docs/ai/product.md`](docs/ai/product.md).

## Commits and pull requests

- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): description`, with a body that says **why**. See [`docs/ai/commit-conventions.md`](docs/ai/commit-conventions.md). The `commit-msg` hook checks the subject line.
- Keep each commit to one logical change, with the build and tests passing.
- Update the docs in the same commit when behaviour, layout, dependencies or versions change, including the [README](README.md) and [`docs/ai/structure.md`](docs/ai/structure.md)'s layout tree.
- Add a line under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) for anything a user would notice.
- Markdown must pass `rumdl check .` (CI runs it).

Every pull request runs **Build and test**: a Release build with warnings as errors, the tests, a NuGet vulnerability check, and a packed, verified `.lplug4` you can download from the run's artifacts and install to try the change on a device.

## Releases

Releases are made by the maintainer by pushing a version tag; see [`docs/ai/release-process.md`](docs/ai/release-process.md).

## Licence

By contributing you agree that your contribution is released under the [MIT License](LICENSE). Don't add xkcd comic images to the repo: the comics are Randall Munroe's work under CC BY-NC 2.5, and the plugin only ever shows them from xkcd's own servers.
