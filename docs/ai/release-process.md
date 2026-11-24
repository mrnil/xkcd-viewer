# Release Process

How to prepare and publish a release of the xkcd viewer plugin. Pushing a version tag runs `.github/workflows/release.yml`, which builds, tests and packs the plugin on GitHub and publishes a GitHub release with the notes and the `.lplug4` attached. Nothing needs building or packing locally.

CI gets `PluginApi.dll` by silently installing Logi Options+ from Logitech's download (`.github/actions/install-pluginapi`), after checking its Logitech signature. The DLL is Logitech's proprietary binary: it's never committed, never shipped in the package, and only exists on the throwaway runner. See `tech.md` → CI/CD.

## Prerequisites

- All changes are on `main`, and the latest **Build and test** run on `main` passed
- The release has been tried on a device: install the `.lplug4` from that run's artifacts (step 7 below) on each device you can (MX keypad, Live S, CT). `supportedDevices` doesn't gate the MX keypad; see `product.md` → "Manifest: devices and platforms"
- `README.md` describes the new behaviour
- `LoupedeckPackage.yaml` has a real `description`, `author` and `copyright` (no template placeholders)
- `EULA.md` still describes what the plugin sends and stores (section 3), and its date is current if it changed. Logitech's Marketplace Approval Guidelines require a Developer EULA with every product

## Steps

### 1. Update the version

Set `version:` in `src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml`, using Semantic Versioning: **major** for breaking changes (for example renaming an action class, which loses users' placed buttons), **minor** for features, **patch** for fixes only.

That's the only place to change it: the `.csproj` reads it, so the DLL and the package always carry the same version. It must be three-part (`1.2.0`): the build fails otherwise, so there are no pre-release versions. The release workflow stops if it doesn't match the tag.

### 2. Update CHANGELOG.md

Turn the `## [Unreleased]` section into `## [{VERSION}] - {DATE}` (date as `YYYY-MM-DD`), start a new, empty `## [Unreleased]` above it, and add a `[{VERSION}]: https://github.com/mrnil/xkcd-viewer/releases/tag/v{VERSION}` link at the bottom. Use [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) sections (`### Added`, `### Changed`, `### Fixed`, `### Removed`, `### Security`). The release workflow stops if there's no dated `## [{VERSION}]` heading.

### 3. Write release notes

Create `RELEASE_NOTES_v{VERSION}.md` in the repo root, written for users rather than developers: what's new, what's fixed, anything they need to do, how to install, and the requirements (Windows, Logi Options+ or the Loupedeck software, an internet connection). Keep the "unofficial, not affiliated with xkcd" line and the xkcd credit. `RELEASE_NOTES_v1.0.0.md` is a model.

The first line is the release title, and must be exactly:

```markdown
# v{VERSION} - {TITLE}
```

with `{TITLE}` replaced by a short title, for example `# v1.1.0 - Bookmarks`. The workflow uses that line as the GitHub release title and the rest of the file as the release body. It stops if the file is missing, if the first line isn't in that form, or if the title still contains `{` or `}`.

### 4. Commit and push

```bash
git add src/xkcdviewerPlugin/package/metadata/LoupedeckPackage.yaml CHANGELOG.md RELEASE_NOTES_v{VERSION}.md
git commit -m "chore(release): prepare v{VERSION}"
git push origin main
```

Wait for **Build and test** on that commit to pass before tagging. Tag the commit you pushed last: CI only runs on the last commit of a push, so an earlier commit from the same push has no checks and the release stops.

### 5. Tag and push the tag

```bash
git tag -a v{VERSION} -m "Release v{VERSION}"
git push origin v{VERSION}
```

Pushing the tag starts the **Release** workflow. It:

1. Checks that the tagged commit is on `main` and that every check on it passed (job `ci-passed`). It needs `build-and-test` to be present, waits up to 30 minutes for checks still running, and stops if any check failed or was cancelled. Nothing below runs unless this passes
2. Checks the tag format, that it matches `LoupedeckPackage.yaml`, the changelog entry, and the release notes file and its title line
3. Installs Logi Options+ for `PluginApi.dll`, builds in Release with warnings as errors, and runs the tests
4. Packs `xkcdviewer_{FILE_VERSION}.lplug4` with the LogiPluginTool version pinned in `.config/dotnet-tools.json`, verifies it and prints its metadata
5. Checks the package: its `LoupedeckPackage.yaml` has the tag's version, and its only DLL is `xkcdviewerPlugin.dll` (no `PluginApi.dll` or its dependencies)
6. Publishes the GitHub release for the tag, marked **Latest**

`{FILE_VERSION}` is the version with its dots replaced by underscores: version `1.0.0` makes `xkcdviewer_1_0_0.lplug4`. That's the `pluginName_version.lplug4` pattern the Logitech Marketplace asks for (its example is `SpotifyPremium_1_0.lplug4`). Everything else (the manifest, the changelog, the `v{VERSION}` tag) keeps the dots.

### 6. Check the published release

1. Open the Release run under **Actions** and read `Package, verify and show metadata`: check the version, display name, author, support page and supported platforms
2. Open **Releases** and read the notes as they render
3. Download the `.lplug4` and install it (step 7) for a last check

### 7. Install the package on a device

Run `dotnet clean xkcdviewerPlugin.sln` first: it deletes the development `.link` file, which would otherwise shadow the installed package (and `logiplugintool install` fails with "Plugin installation cannot start" while it exists). Then double-click the `.lplug4`, or:

```bash
dotnet logiplugintool install ./xkcdviewer_{FILE_VERSION}.lplug4
```

Check the actions on each device. `dotnet logiplugintool uninstall xkcdviewer` removes it; a Debug build puts the development link back.

### 8. Submit to the Logitech Marketplace (manual)

Submitting is a separate, manual step. Upload the release's `.lplug4`, provide `EULA.md` as the Developer EULA (link it from the listing, or supply it wherever the submission asks for one), and put the xkcd attribution and "unofficial" disclaimer from the README in the listing description.

## Fixing a failed or wrong release

The release is created only after every check, the tests and the package verification pass, so a failed run leaves nothing on the Releases page. Fix the problem, then move the tag to the new commit:

```bash
git push origin :refs/tags/v{VERSION}   # delete the remote tag
git tag -d v{VERSION}
# commit and push the fix to main, wait for CI, then repeat step 5
```

If the run succeeded but the notes or title need a fix, edit the release on GitHub (or `gh release edit v{VERSION} --title ... --notes-file ...`) and commit the same change to `RELEASE_NOTES_v{VERSION}.md`. Don't move the tag of a published release: if the package itself is wrong, release a new patch version.

## Checklist

- [ ] Latest **Build and test** run on `main` passed
- [ ] Tried on each available device from the CI artifact
- [ ] Template placeholders gone from `LoupedeckPackage.yaml` (`description`, `author`, `copyright`)
- [ ] `version:` updated in `LoupedeckPackage.yaml` (three-part; the `.csproj` follows it)
- [ ] `CHANGELOG.md` entry, dated, with a link and a new empty `[Unreleased]` section
- [ ] `RELEASE_NOTES_v{VERSION}.md`, first line `# v{VERSION} - {TITLE}` with a real title
- [ ] `README.md` up to date; `EULA.md` checked against the plugin's network and storage behaviour
- [ ] Committed and pushed, CI passed
- [ ] Tag `v{VERSION}` pushed
- [ ] Release workflow passed, and the published release's notes, metadata and package checked
- [ ] Submitted to the Marketplace with `EULA.md`, if this release is going there

## Building a package locally

Only needed to try a package before tagging. Every push to `main` and every pull request also uploads an untagged `xkcdviewer.lplug4` as an artifact of the **Build and test** run (kept 14 days).

```bash
dotnet build xkcdviewerPlugin.sln -c Release
dotnet tool restore
dotnet logiplugintool pack ./bin/Release ./xkcdviewer_{FILE_VERSION}.lplug4
dotnet logiplugintool verify ./xkcdviewer_{FILE_VERSION}.lplug4
dotnet logiplugintool metadata ./xkcdviewer_{FILE_VERSION}.lplug4
```

The Release build repoints the `.link` file at `bin/Release/` and reloads the plugin (pass `-p:PluginDir=<some other folder>/` to leave your Debug link alone; run a Debug build afterwards to go back to development). `bin/Release/bin/` should hold only `xkcdviewerPlugin.dll`, its `.deps.json` and `.pdb`, and the package should be well under 1 MB (about 65 KB at 1.0.0). The build copies package files but never deletes them, so a file removed from `src/xkcdviewerPlugin/package/` stays in `bin/Release/` until you delete it. The release workflow always builds from a clean checkout. `*.lplug4` is git-ignored: attach packages to releases, never commit them.

## LogiPluginTool Commands Reference

| Command | Purpose |
| --- | --- |
| `dotnet logiplugintool pack <bin/Release dir> <output.lplug4>` | Build the package |
| `dotnet logiplugintool verify <package>` | Unpack and check for errors/missing files |
| `dotnet logiplugintool metadata <package>` | Print the manifest as JSON |
| `dotnet logiplugintool install <package>` | Install into Logi Plugin Service |
| `dotnet logiplugintool uninstall xkcdviewer` | Remove it |

## Troubleshooting

- **"Tag ... isn't vMAJOR.MINOR.PATCH"**: tags must look like `v1.2.0`, with no suffix.
- **"Tag ... doesn't match version: ... in LoupedeckPackage.yaml"**: step 1 wasn't done, or not on the tagged commit.
- **"CHANGELOG.md has no ... entry" or "RELEASE_NOTES ... is missing"**: the file wasn't committed, or its heading (with its date) or name doesn't use the tag's version.
- **"The first line of RELEASE_NOTES ... should be" or "still has a placeholder"**: fix the first line, commit and push, then move the tag.
- **"isn't on main"**: the tag points at a commit never pushed to `main`. Push it, wait for CI, then move the tag.
- **"No build-and-test check"**: CI never ran on the tagged commit, usually because it wasn't the last commit of a push. Move the tag to the last commit pushed. If the job in `build.yml` was renamed, update `REQUIRED` in `release.yml`.
- **"Checks on ... did not pass"**: a CI run on the tagged commit failed or was cancelled. Rerun it if it was flaky; otherwise fix it on `main` and move the tag. Then rerun the Release workflow.
- **"Installer signature check failed" or "PluginApi.dll not found after install"**: Logitech changed the Options+ installer, its signing certificate, or where it puts Logi Plugin Service. The step prints the installer log on failure. Update `.github/actions/install-pluginapi/action.yml`.
- **"The package must not contain PluginApi.dll" or "should hold only xkcdviewerPlugin.dll"**: the `PluginApi` reference in `Directory.Build.props` lost `<Private>false</Private>` for the plugin project.
- **Build fails with `CS1705`**: a Logi Options+ update moved `PluginApi.dll` to a newer .NET; see `tech.md`.
- **Verify fails**: check the manifest keys against the SDK docs and that `pluginFileName` (`xkcdviewerPlugin.dll`) exists under `pluginFolderWin` (`bin`).
- **"a release with the same tag name already exists"**: edit that release rather than rerunning, or delete it first if it was never meant to be published.
