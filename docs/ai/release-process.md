# Release Process

How to create and publish a new release of the xkcd viewer plugin.

## Prerequisites

- All code changes committed to git
- All tests passing
- Documentation updated (README, CHANGELOG)
- `LoupedeckPackage.yaml` has a real `description`, `author` and `copyright` (the template placeholders must not ship)

## Release Steps

### 1. Update Version Number

In `src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml`:

```yaml
version: 1.0.0
```

Also update `<Version>` in `src/xkcdviewerPlugin/xkcdviewerPlugin.csproj` (add it on the first release). The two must match.

### 2. Build Release Configuration

```bash
dotnet build xkcdviewerPlugin.sln -c Release
```

Output: `bin/Release/bin/xkcdviewerPlugin.dll` plus dependencies, and `bin/Release/metadata/`. The post-build step also repoints the `.link` file at `bin/Release/` and reloads the plugin. Run a Debug build afterwards to go back to development.

### 3. Update CHANGELOG.md

Add a `## [{VERSION}] - {DATE}` entry at the top, in [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format (`### Added` / `### Changed` / `### Fixed` / `### Security`).

### 4. Commit and Tag

```bash
git add CHANGELOG.md src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml src/xkcdviewerPlugin/xkcdviewerPlugin.csproj
git commit -m "chore(release): v{VERSION}"
git tag -a v{VERSION} -m "Release v{VERSION}"
```

### 5. Create the .lplug4 Package

```bash
LogiPluginTool pack ./bin/Release ./xkcdviewer-v{VERSION}.lplug4
```

`*.lplug4` is git-ignored. Attach the package to the release rather than committing it. See `tech.md` for which `LogiPluginTool` binary to use.

### 6. Verify the Package

```bash
LogiPluginTool verify ./xkcdviewer-v{VERSION}.lplug4
LogiPluginTool metadata ./xkcdviewer-v{VERSION}.lplug4
```

Check the version, display name and supported platforms.

### 7. Test Install

```bash
LogiPluginTool install ./xkcdviewer-v{VERSION}.lplug4
```

Run `dotnet clean` first so the development `.link` file doesn't shadow the installed package. Check the actions on each target device.

### 8. Push and Publish

```bash
git push origin main
git push origin v{VERSION}
```

Create a GitHub release from the tag and attach the `.lplug4`.

## LogiPluginTool Commands Reference

| Command | Purpose |
|---------|---------|
| `LogiPluginTool pack <bin/Release dir> <output.lplug4>` | Build the package |
| `LogiPluginTool verify <package>` | Unpack and check for errors/missing files |
| `LogiPluginTool metadata <package>` | Print the manifest as JSON |
| `LogiPluginTool install <package>` | Install into Logi Plugin Service |
| `LogiPluginTool uninstall xkcdviewer` | Remove it |

## Version Numbering

Semantic Versioning: **major** for breaking changes, **minor** for backward-compatible features, **patch** for fixes.

## Checklist

- [ ] All code committed, all tests passing, build at zero warnings
- [ ] Template placeholders gone from `LoupedeckPackage.yaml` (`description`, `author`, `copyright`)
- [ ] Version updated in `LoupedeckPackage.yaml` and `.csproj`, and they match
- [ ] `supportedDevices` checked against the devices actually tested (see the SDK docs' supported-devices page)
- [ ] CHANGELOG.md updated
- [ ] Git tag created and pushed
- [ ] `.lplug4` packed, verified, metadata checked, test-installed
- [ ] GitHub release created with the `.lplug4` attached

## Troubleshooting

- **Pack fails**: check that the Release build succeeded and `bin/Release/bin` and `bin/Release/metadata` exist.
- **Verify fails**: check the manifest keys against the SDK docs and that `pluginFileName` (`xkcdviewerPlugin.dll`) exists under `pluginFolderWin` (`bin`).
- **Version mismatch**: update both version locations, rebuild Release, repack.
