# Action Editor Command Pattern

`ActionEditorCommand` / `ActionEditorAdjustment` create actions the **user configures** per button through controls (textbox, listbox, checkbox, slider) in the Loupedeck/Options+ software, e.g. "open comic #___".

**Read first:** an `ActionEditorCommand`'s custom image never reaches an MX Keypad (see `sdk-quick-reference.md` → Known Device Gotchas). Use this base class only for actions whose display name alone is enough. Anything that needs a drawn image belongs on `PluginDynamicCommand`.

## Signatures

`ActionEditorCommand` is **not** a `PluginDynamicCommand`: it's a separate hierarchy (`ActionEditorCommand` → `ActionEditorAction` → `PluginAction`), and its overrides take the parameters object:

```csharp
protected override String GetCommandDisplayName(ActionEditorActionParameters actionParameters);
protected override BitmapImage GetCommandImage(ActionEditorActionParameters actionParameters, Int32 imageWidth, Int32 imageHeight);
protected override Boolean RunCommand(ActionEditorActionParameters actionParameters);
protected void ActionImageChanged(); // re-invokes both display name and image
```

Both render overrides must be synchronous. Cache any async-resolved value and call `ActionImageChanged()` when it arrives.

## Example: Open a Specific Comic

```csharp
public class OpenComicCommand : ActionEditorCommand
{
    private const String ComicNumberControlName = "ComicNumber";

    public OpenComicCommand()
    {
        this.Name = "OpenComic";
        this.DisplayName = "Open Comic";
        this.GroupName = "Comics";
        this.Description = "Opens a specific xkcd comic";

        this.ActionEditor.AddControlEx(new ActionEditorTextbox(ComicNumberControlName, "Comic number"));
    }

    protected override Boolean OnLoad() => true;

    protected override String GetCommandDisplayName(ActionEditorActionParameters actionParameters)
        => actionParameters.TryGetString(ComicNumberControlName, out var number) && !String.IsNullOrEmpty(number)
            ? $"xkcd #{number}"
            : "Open Comic";

    protected override Boolean RunCommand(ActionEditorActionParameters actionParameters)
    {
        if (!actionParameters.TryGetString(ComicNumberControlName, out var text)
            || !Int32.TryParse(text, out var number)
            || number < 1)
        {
            PluginLog.Warning($"Open Comic: '{text}' is not a valid comic number");
            return false;
        }

        xkcdviewerPlugin.Instance?.OpenComic(number);
        return true;
    }
}
```

`ActionEditorAdjustment` is the dial equivalent: `base(hasReset: false)` and `protected override Boolean ApplyAdjustment(ActionEditorActionParameters actionParameters, Int32 diff)`.

## Controls

| Control | Use |
|---------|-----|
| `ActionEditorTextbox` | Free text. Validate everything read from it |
| `ActionEditorListbox` | Dropdown whose items are supplied **at runtime** (below) |
| `ActionEditorCheckbox` | Boolean |
| `ActionEditorSlider` | Number in a range |

Read values back with `actionParameters.TryGetString(controlName, out var value)` (also `TryGetInt32`). A listbox's selected item's `name` comes back the same way.

### Dynamic Listbox

The SDK raises `ListboxItemsRequested` each time the editor needs the list:

```csharp
this.ActionEditor.AddControlEx(new ActionEditorListbox(FavouriteControlName, "Favourite:"));
this.ActionEditor.ListboxItemsRequested += this.OnListboxItemsRequested;

private void OnListboxItemsRequested(Object sender, ActionEditorListboxItemsRequestedEventArgs e)
{
    if (!e.ControlName.EqualsNoCase(FavouriteControlName))
    {
        return;
    }

    foreach (var favourite in this._cachedFavourites)
    {
        e.AddItem(favourite.Num.ToString(), favourite.Title, description: null);
    }
}
```

The handler must be synchronous. Load the data in the background beforehand and read from the cache here.

## Rules

1. Keep control names in `const` fields
2. Validate every value; it's user input (`secure-coding.md`)
3. Mark optional controls as such in their labels
4. Return `false` from `RunCommand` when the configuration is unusable, and log why
5. Do network work in `Task.Run`, not inline

## Caveat: User-Set Captions

A caption the user types for a button permanently replaces `GetCommandDisplayName`'s value. Don't rely on the dynamic display name for anything the user must always see.

## Choosing a Pattern

| Need | Use |
|------|-----|
| User types or picks a value per button | `ActionEditorCommand` |
| One button per item from a runtime list | `PluginDynamicFolder`, or `PluginDynamicCommand` with `AddParameter` per item |
| A small fixed set of actions | `PluginDynamicCommand` with fixed parameters |
| Custom drawn image | `PluginDynamicCommand` (not `ActionEditorCommand`) |
