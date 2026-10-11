# Changelog

All notable changes to xkcd viewer are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-11

The first release: an xkcd comic split across a 3×2 block of keys, with Prev / Next above it.

### Added

- Three folder actions in the **xkcd viewer** group in Logi Options+:
  - **Comic Browser**, which opens at the last comic viewed (the newest one on first use)
  - **Latest Comic**, which opens at the newest comic published
  - **Random Comic**, which opens at a random comic and has a "show another" key
- Prev / Next keys: a tap steps one comic, and a 2 second hold (or the long press on the Live S / CT touch keys) jumps to the first or the newest comic. The chevrons dim when there's nowhere further to go.
- A **New Comic** key: it flashes green with "new" when xkcd publishes a comic, stays green until that comic has been shown on the keypad, and opens it in the browser when pressed. In the Comic Browser, the Next chevron turns green too, and a tap goes to the first unseen comic.
- A check for new comics on xkcd's release days (Monday, Wednesday and Friday), at most once an hour in the US afternoon and evening, plus one each time the plugin starts.
- Pressing any comic tile opens that comic's page on xkcd.com.
- Title cards for comics that don't work as six tiles: interactive, very tall or very large ones.
- Layouts for the MX Creative Console keypad (3×3), Loupedeck Live S (5×3) and Loupedeck CT (4×3); other devices use the 3×3 layout.
- Preloading of the comics either side of the current one, and in-memory caches for comic details and images.
- "loading…" and "offline" placeholders, with failed requests retried at most every 30 seconds.
- The last comic viewed, the newest comic shown and the newest comic known are remembered between sessions, in the plugin's local settings.

### Security

- Only `xkcd.com` and `imgs.xkcd.com` are requested or opened. Redirects aren't followed, every request has a timeout, and responses are size-capped.
- Every field from xkcd's API is treated as untrusted and validated before use.

[1.0.0]: https://github.com/mrnil/xkcd-viewer/releases/tag/v1.0.0
