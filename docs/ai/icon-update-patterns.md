# Icon Update Patterns

How to get a button or dial redrawn when state changes. The SDK caches what it drew, so nothing changes on the device until you tell it to.

## Which Refresh Call (verified by reflection against PluginApi 6.4.1)

| Base class | Redraw one parameter | Redraw everything | Re-list buttons/parameters |
| --- | --- | --- | --- |
| `PluginDynamicCommand` | `ActionImageChanged(actionParameter)` | `ActionImageChanged()` | `ParametersChanged()` (after `RemoveAllParameters`/`AddParameter`) |
| `PluginDynamicAdjustment` | `AdjustmentValueChanged(actionParameter)` | `AdjustmentValueChanged()` | `ParametersChanged()` |
| `PluginDynamicFolder` | `CommandImageChanged(actionParameter)` / `AdjustmentValueChanged(actionParameter)` | none | `ButtonActionNamesChanged()` / `EncoderActionNamesChanged()` |
| `ActionEditorCommand` | none | `ActionImageChanged()` (re-invokes both display name and image) | none |

`CommandImageChanged` exists **only** on `PluginDynamicFolder`. Many examples online use it on commands, and that doesn't compile.

**A folder's own key can't be redrawn.** The image `GetButtonImage` returns for the key that opens a folder stays as first drawn: there's no refresh call for it, and re-listing the plugin's actions to force one flashed a warning triangle on the keypad and still showed the old image (`product.md` → "The new-comic signal"). Anything that must change goes on a `PluginDynamicCommand` key instead, as the New Comic key does.

**Redraw only what changed.** Re-listing (`ParametersChanged`, `ButtonActionNamesChanged`) rebuilds every button, so use it only when the *set* of buttons changes, not to redraw one.

## Selection Change (update the old and the new)

For mutually exclusive items (e.g. a folder of favourite comics with the current one highlighted), redraw both the previously selected item and the newly selected one:

```csharp
public void OnCurrentComicChanged(String number)
{
    var previous = this._current;
    this._current = number ?? String.Empty;

    if (!String.IsNullOrEmpty(previous) && previous != this._current)
    {
        this.CommandImageChanged(previous);
    }

    if (!String.IsNullOrEmpty(this._current))
    {
        this.CommandImageChanged(this._current);
    }
}
```

## Refresh After Async Work Completes, Not On Press

When a press starts a fetch, redraw after the data arrives. A redraw in `RunCommand` itself runs before the fetch finishes and shows stale data:

```csharp
// WRONG - GetCommandImage runs before the comic has loaded
protected override void RunCommand(String actionParameter)
{
    xkcdviewerPlugin.Instance?.LoadRandom();
    this.ActionImageChanged(); // too early
}

// RIGHT - redraw when the plugin reports the new comic
protected override void RunCommand(String actionParameter)
{
    xkcdviewerPlugin.Instance?.LoadRandom(); // raises ComicChanged when done
}

private void OnComicChanged(Object sender, EventArgs e) => this.ActionImageChanged();
```

It's fine to redraw immediately to show a "loading…" state, as long as a second redraw follows when the result arrives.

## Event-Driven Refresh

When the plugin raises an event after its state has already changed, redraw directly, with no delay. In this plugin, `NewComicCommand` redraws on `xkcdviewerPlugin.NewComicLookChanged` and `ComicFolder` on `ComicService.Changed`:

```csharp
public void OnComicChanged() => this.ActionImageChanged();
```

Render methods then read the current state (see `guidelines.md` → "Render Methods Read State").

Never paper over ordering problems with `Task.Delay` before a redraw. Refresh from the completion of the work, which the plugin controls here since xkcd is a pull-only API.

## Event Flow Example: "Random Comic" Button

1. User presses the button → `RunCommand`
2. The command calls `xkcdviewerPlugin.Instance?.LoadRandom()`, which returns immediately and fetches in `Task.Run`
3. The fetch completes, the plugin stores the comic and raises `ComicChanged`
4. Every interested command calls `ActionImageChanged()` (or `AdjustmentValueChanged()`)
5. The SDK calls `GetCommandImage`, which reads the stored comic and draws it
