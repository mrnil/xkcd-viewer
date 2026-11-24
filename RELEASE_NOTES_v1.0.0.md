# v1.0.0 - First release

The first release of xkcd viewer: the current xkcd comic split across your keypad, with Prev / Next keys above it. Press any part of the comic to open it on xkcd.com.

This is an unofficial fan project. It isn't made by or affiliated with xkcd or Randall Munroe.

## What's in it

- **Three folder actions** in the **xkcd viewer** group in Logi Options+:
  - **Comic Browser** opens at the last comic you viewed (the newest one on first use)
  - **Latest Comic** opens at the newest comic published
  - **Random Comic** opens at a random comic, with a "show another" key
- **A New Comic key**: the logo with its eyes closed, until xkcd publishes a new comic. Then it flashes green with "new", stays green until you've seen the comic, and opens it in your browser when pressed.
- **Prev / Next**: tap to step one comic, or hold for 2 seconds to jump to the first or the newest.
- **Title cards** for comics that don't work as six tiles: interactive, very tall or very large ones.
- **Works offline gracefully**: keys show "loading…" or "offline" rather than freezing.

## Supported devices

| Device | Layout |
| --- | --- |
| MX Creative Console keypad | 3 × 3 |
| Loupedeck Live S | 5 × 3, comic centred |
| Loupedeck CT | 4 × 3 |

Other devices use the 3 × 3 layout.

## Requirements

- Windows (macOS hasn't been tried yet)
- Logi Options+ or the Loupedeck software, with Logi Plugin Service
- An internet connection: comics come from xkcd.com

## Installing

Download `xkcdviewer_1_0_0.lplug4` below and open it. Logi Options+ (or the Loupedeck software) asks you to confirm the install. Then drag one of the **xkcd viewer** actions onto a key.

## Privacy

The plugin only talks to `xkcd.com` and `imgs.xkcd.com`. It stores three comic numbers in its local Logi settings (the last comic viewed, and the newest comic shown and known) and nothing else. See the [end user licence agreement](https://github.com/mrnil/xkcd-viewer/blob/main/EULA.md).

## Credits

Comics by Randall Munroe at [xkcd.com](https://xkcd.com/), licensed under [CC BY-NC 2.5](https://creativecommons.org/licenses/by-nc/2.5/).
