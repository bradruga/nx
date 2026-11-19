using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Nx.Components;

public partial class NxComboBox<TItem, TValue>
{
    private bool HasActiveItem => isOpen && activeRow >= 0 && activeRow < rows.Count && !rows[activeRow].IsHeader;

    /// <summary>
    /// With <see cref="Multiple"/>, Space toggles the highlighted item once the user has arrowed to it;
    /// before that it types a space. nx-combobox.js applies the same rule to cancel the key's default.
    /// </summary>
    private bool SpaceToggles => Multiple && navigated && HasActiveItem;

    private bool ActiveMatchesText() =>
        string.Equals(TextOf(rows[activeRow].Item), FilterText.Trim(), StringComparison.OrdinalIgnoreCase);

    // @bind, not value + @oninput: only @bind tells the renderer the event updates the value, which
    // stops a slow render (filtering a long list) from writing stale text over keystrokes.
    private async Task HandleInputAfter()
    {
        inputText ??= "";
        if (!Editable || UseOverlay)
            return;
        if (Suggest && !CanSuggest)
        {
            Close();
            return;
        }
        if (!isOpen || DataProvider is null)
        {
            await OpenAsync(filtering: true);
            return;
        }
        isFiltering = true;
        await LoadAsync(inputText, debounce: true);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!Editable)
            return;

        // In mobile mode the field only opens the overlay, which has its own search box.
        if (UseOverlay)
        {
            if (e.Key is "Enter" or "ArrowDown" or " ")
                await OpenOverlayAsync();
            return;
        }

        if (PickOnly && await HandlePickKeyAsync(e))
            return;

        if (await HandleListKeyAsync(e))
            return;

        switch (e.Key)
        {
            case "ArrowDown" when !isOpen && Suggest:
                if (CanSuggest)
                    await OpenAsync(filtering: true);
                break;
            case "ArrowDown" when !isOpen:
                await OpenAsync(filtering: false);
                break;
            case "Enter":
                if (!Multiple)
                    await CommitTypedTextAsync();
                break;
            case "Escape" when !isOpen:
                if (Clearable)
                    await ClearAsync();
                break;
            case "Backspace":
                if (Multiple && inputText.Length == 0 && chips.Count > 0)
                    await RemoveChipAsync(chips.Count - 1);
                break;
        }
    }

    /// <summary>Keys that act on an open list, in the popup or the overlay. Returns <c>true</c> when handled.</summary>
    private async Task<bool> HandleListKeyAsync(KeyboardEventArgs e)
    {
        if (!isOpen)
            return false;

        switch (e.Key)
        {
            case "ArrowDown" when !e.AltKey:
                Move(+1);
                return true;
            case "ArrowUp" when e.AltKey && !overlayOpen:
                await CommitTypedTextAsync();
                return true;
            case "ArrowUp":
                Move(-1);
                return true;
            case "PageDown":
                Move(+PageSize);
                return true;
            case "PageUp":
                Move(-PageSize);
                return true;
            case "Enter":
                if (HasActiveItem && rows[activeRow].Special != SpecialRow.None)
                    await ActivateRowAsync(rows[activeRow]);
                else if (Multiple)
                {
                    if (HasActiveItem)
                        await ToggleAsync(rows[activeRow].Item);
                }
                // With custom values allowed, Enter keeps the typed text unless the user picked an item.
                else if (HasActiveItem && (navigated || !AllowCustomValue || ActiveMatchesText()))
                    await PickAsync(rows[activeRow].Item);
                else if (!overlayOpen)
                    await CommitTypedTextAsync();
                return true;
            case " " when SpaceToggles:
                await ActivateRowAsync(rows[activeRow]);
                return true;
            case "Escape":
                if (overlayOpen)
                {
                    await CloseOverlayAsync(commit: false);
                }
                else
                {
                    // Suggestions close and leave the typed text; a combo box reverts to its selection.
                    if (!Suggest)
                        inputText = DisplayText;
                    Close();
                }
                return true;
        }
        return false;
    }

    private void Move(int delta)
    {
        if (itemCount == 0)
            return;

        var step = Math.Sign(delta);
        var remaining = Math.Abs(delta);
        var row = activeRow < 0 ? (step > 0 ? -1 : rows.Count) : activeRow;
        var target = activeRow;
        while (remaining > 0)
        {
            row += step;
            if (row < 0 || row >= rows.Count)
                break;
            if (rows[row].IsHeader)
                continue;
            target = row;
            remaining--;
        }
        if (target < 0)
            target = rows.FindIndex(r => !r.IsHeader);
        activeRow = target;
        navigated = true;
        scrollPending = true;
    }

    private void HandleFocus() => hasFocus = true;

    private async Task HandleBlur()
    {
        hasFocus = false;
        // Typed text is committed even when the suggestions closed for want of matches.
        if (Editable && !overlayOpen && (isOpen || isFiltering || (Suggest && inputText != DisplayText)))
            await CommitTypedTextAsync();
    }

    private async Task HandleFieldClick()
    {
        await ViewportKnownAsync();
        if (UseOverlay && Editable && !isOpen)
            await OpenOverlayAsync();
        else if (PickOnly && !UseOverlay && Editable)
            await HandleToggleClick();
    }

    private async Task HandleToggleClick()
    {
        if (UseOverlay)
        {
            await OpenOverlayAsync();
            return;
        }
        if (isOpen)
        {
            inputText = DisplayText;
            Close();
        }
        else
        {
            await OpenAsync(filtering: false);
        }
        await input.FocusAsync();
    }

    private async Task HandleClearClick()
    {
        await ClearAsync();
        if (!UseOverlay)
            await input.FocusAsync();
    }

    /// <summary>A click or Enter on an option: toggles it with <see cref="Multiple"/>, else selects it and closes.</summary>
    private Task HandleOptionClick(TItem item) => Multiple ? ToggleAsync(item) : PickAsync(item);

    private async Task PickAsync(TItem item)
    {
        var fromOverlay = overlayOpen;
        await SelectAsync(item);
        if (fromOverlay)
            returnFocusPending = true;
    }
}
