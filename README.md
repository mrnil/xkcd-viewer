# xkcd viewer

A plugin for Logitech and Loupedeck control surfaces that shows an xkcd comic **across the device's keys**. The comic is split into six tiles over a 3×2 block of keys, with Prev / Next keys above it, so the whole keypad becomes a small picture of the comic. Press any part of the comic to open it on xkcd.com in your browser.

It's an at-a-glance viewer: xkcd is text-heavy, and most speech bubbles are unreadable at key size, so reading a comic properly is one key press away.

This is an unofficial fan project. It isn't made by or affiliated with xkcd or Randall Munroe.

## Features

- **Three folder actions**, in the **xkcd viewer** group in Logi Options+:

  | Action | Opens at |
  |--------|----------|
  | **Comic Browser** | The last comic you viewed (the newest one on first use) |
  | **Latest Comic** | The newest comic published |
  | **Random Comic** | A random comic, with a "show another" key |

- **Prev / Next**: tap to step one comic; **hold for 2 seconds** to jump to the first comic (Prev) or the newest (Next). The chevrons dim when there's nowhere further to go.
- **Open in the browser**: pressing any comic tile opens `https://xkcd.com/{number}/`.
- **Awkward comics get a title card**: comics that are interactive, very tall or very large, and wouldn't make sense as a six-key thumbnail, show their number and title instead.
- **Fast**: the comics either side of the current one are fetched in advance, so Prev / Next usually change at once.
- **Remembers where you were**: the last comic viewed is saved and reopened next time.
- **Behaves offline**: keys show "loading…" or "offline" rather than freezing, and failed requests aren't retried on every press.

## Supported devices

| Device | Layout |
|--------|--------|
| MX Creative Console keypad (primary target) | 3 × 3 |
| Loupedeck Live S | 5 × 3, comic centred |
| Loupedeck CT | 4 × 3 |

The device's own Back key takes the top-left key. Other devices fall back to the 3 × 3 layout until someone tests one.

```text
MX Creative Keypad         Loupedeck Live S                  Loupedeck CT
[Back][Prev][Next]         [Back][Prev][Next][    ][    ]    [Back][Prev][Next][    ]
[ c0 ][ c1 ][ c2 ]         [    ][ c0 ][ c1 ][ c2 ][    ]    [ c0 ][ c1 ][ c2 ][    ]
[ c3 ][ c4 ][ c5 ]         [    ][ c3 ][ c4 ][ c5 ][    ]    [ c3 ][ c4 ][ c5 ][    ]
```

## Status

The v1 features are built and being tested on hardware. There's no packaged release yet, so for now you install it by building it from source (below). What's left to check before a release is in [`docs/ai/product.md`](docs/ai/product.md), under "Hardware test checklist" and "Still open".

## Building and running

You'll need:

- Windows (macOS isn't set up yet)
- [Logi Options+](https://www.logitech.com/software/logi-options-plus.html), which installs the Logi Plugin Service and its `PluginApi.dll`
- The .NET 10 SDK

```bash
dotnet build xkcdviewerPlugin.sln
```

The build writes a `.link` file that points the Logi Plugin Service at `bin/Debug/`, and reloads the plugin. The **xkcd viewer** actions then appear in Logi Options+; drag one onto a key. `dotnet clean` removes the link again.

Run the tests with:

```bash
dotnet test
```

Packaging and release steps are in [`docs/ai/release-process.md`](docs/ai/release-process.md).

## How it works

The plugin reads xkcd's public JSON API (`https://xkcd.com/info.0.json` and `https://xkcd.com/{number}/info.0.json`), downloads the comic image, fits it into a 3:2 area and cuts that into one tile per key. Everything is cached in memory only, and nothing is written to disk apart from the number of the last comic viewed.

Every response from xkcd is treated as untrusted. Only `xkcd.com` and `imgs.xkcd.com` URLs are requested or opened, redirects aren't followed, and responses are size-capped. See [`docs/ai/secure-coding.md`](docs/ai/secure-coding.md).

## Project layout

```text
src/xkcdviewerPlugin/          The plugin (Logi Actions SDK, C# / .NET 10)
  Actions/                     The folder actions: Comic Browser, Latest Comic, Random Comic
  Services/                    The xkcd client, the comic service and its caches
  Helpers/, Models/            Layout, fitting, title cards, key rendering
  package/metadata/            The plugin manifest and package icon
tests/xkcdviewerPlugin.Tests/  xUnit tests, with real API responses as fixtures
art/                           The logo source and the script that generates the icons
docs/ai/                       Design notes, decisions, hardware findings and coding rules
```

[`docs/ai/structure.md`](docs/ai/structure.md) describes every file. If you're contributing, or are an AI assistant, start at [`AGENTS.md`](AGENTS.md).

## Credits

- Comics by Randall Munroe at [xkcd.com](https://xkcd.com/), licensed under [CC BY-NC 2.5](https://creativecommons.org/licenses/by-nc/2.5/). The plugin shows them from xkcd's own servers and always links back to the comic's page.
- The xkcd viewer logo (`art/xkcdviewerlogo.svg`) is the plugin owner's own drawing, not xkcd's logo.
