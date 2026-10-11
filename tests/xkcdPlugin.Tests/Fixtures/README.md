# Fixtures

Real `info.0.json` responses, captured unedited from the live API on 2026-10-11. Each one is here for a reason:

| File | Why |
|------|-----|
| `latest-3309.json` | `https://xkcd.com/info.0.json` at capture time: an ordinary recent comic |
| `1.json` | The first comic; a `.jpg` image with parentheses in its file name |
| `353.json` | An ordinary comic with a long transcript |
| `259.json` | `title` holds an HTML entity (`Clich&eacute;d`), and `safe_title` has lost the character (`Clichd`) |
| `472.json` | `title` holds an HTML `<span>` |
| `1608.json`, `1663.json` | Interactive comics: `img` is `https://imgs.xkcd.com/comics/` with no file name |

Comic 404 has no fixture: as a joke, `https://xkcd.com/404/info.0.json` answers HTTP 404 with an nginx HTML page, and `xkcdClient` treats that one 404 as the comic (see `docs/ai/product.md`, "Data").

Add a new fixture by capturing the real response with `curl -o`, not by writing one by hand.
