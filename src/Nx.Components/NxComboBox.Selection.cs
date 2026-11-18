namespace Nx.Components;

public partial class NxComboBox<TItem, TValue>
{
    /// <summary>Returns whether Value or SelectedItem changed from outside.</summary>
    private bool SyncSingleFromParameters()
    {
        var hadSelection = hasSelection;
        var previousItem = selectedItem;
        var valueChanged = !initialized || !EqualityComparer<TValue>.Default.Equals(Value, lastValue);
        var selectedItemChanged = initialized && !EqualityComparer<TItem>.Default.Equals(SelectedItem, lastSelectedItemParameter);
        lastSelectedItemParameter = SelectedItem;

        // Not "is not null": an unset SelectedItem of a value type (an enum item) is its default, which must not win over Value.
        if ((!initialized || selectedItemChanged) && !EqualityComparer<TItem>.Default.Equals(SelectedItem, default))
            SetSelection(SelectedItem!);
        else if (valueChanged)
            ResolveValue(Value);
        else if (selectedItemChanged)
            ClearSelectionState();
        else if (!hasSelection && customText is null && !EqualityComparer<TValue>.Default.Equals(Value, default))
            ResolveValue(Value); // Data arrived after Value: the item can be found now.

        lastValue = Value;

        // When code sets one of Value and SelectedItem, a bound other one is brought along after render.
        // Only when this pass changed the selection, so a parent that ignores the callback cannot loop.
        var selectionChanged = hasSelection != hadSelection || !EqualityComparer<TItem>.Default.Equals(selectedItem, previousItem);
        syncPending = selectionChanged && hasSelection && (
            (SelectedItemChanged.HasDelegate && !EqualityComparer<TItem>.Default.Equals(SelectedItem, selectedItem)) ||
            (ValueChanged.HasDelegate && !EqualityComparer<TValue>.Default.Equals(Value, ValueOf(selectedItem!))));
        return initialized && (valueChanged || selectedItemChanged);
    }

    private void ResolveValue(TValue? value)
    {
        foreach (var item in source)
        {
            if (EqualityComparer<TValue>.Default.Equals(ValueOf(item), value))
            {
                SetSelection(item);
                return;
            }
        }

        // A provider's item is unknown until loaded; keep a selection that already has this value.
        if (hasSelection && EqualityComparer<TValue>.Default.Equals(ValueOf(selectedItem!), value))
            return;

        ClearSelectionState();
        if (AllowCustomValue && value is string text)
            customText = text;
    }

    private void SetSelection(TItem item)
    {
        hasSelection = true;
        selectedItem = item;
        customText = null;
    }

    private void ClearSelectionState()
    {
        hasSelection = false;
        selectedItem = default;
        customText = null;
    }

    private async Task SelectAsync(TItem item)
    {
        var changed = !IsSelected(item) || customText is not null;
        SetSelection(item);
        inputText = TextOf(item);
        Close();
        if (changed)
            await NotifyAsync(ValueOf(item), item);
    }

    private async Task ClearAsync()
    {
        if (Multiple)
        {
            await ClearChipsAsync();
            return;
        }
        var changed = hasSelection || customText is not null;
        ClearSelectionState();
        inputText = "";
        Close();
        if (changed)
            await NotifyAsync(default, default);
    }

    private async Task CommitCustomAsync(string text)
    {
        var changed = hasSelection || customText != text;
        ClearSelectionState();
        customText = text;
        inputText = text;
        Close();
        if (changed)
            await NotifyAsync((TValue)(object)text, default);
    }

    private async Task NotifyAsync(TValue? value, TItem? item)
    {
        // A bound parameter comes back with the new value; an unbound one keeps its old value,
        // which must not read as an outside change on the next render.
        if (ValueChanged.HasDelegate)
            lastValue = value;
        if (SelectedItemChanged.HasDelegate)
            lastSelectedItemParameter = item;
        notifying = true;
        try
        {
            await ValueChanged.InvokeAsync(value);
            await SelectedItemChanged.InvokeAsync(item);
        }
        finally
        {
            notifying = false;
        }
        validation.NotifyFieldChanged();
    }

    private async Task SyncBindingsAsync()
    {
        if (Multiple)
        {
            await SyncMultipleBindingsAsync();
            return;
        }
        if (!hasSelection)
            return;
        var item = selectedItem!;
        var value = ValueOf(item);
        notifying = true;
        try
        {
            if (SelectedItemChanged.HasDelegate && !EqualityComparer<TItem>.Default.Equals(SelectedItem, item))
            {
                lastSelectedItemParameter = item;
                await SelectedItemChanged.InvokeAsync(item);
            }
            if (ValueChanged.HasDelegate && !EqualityComparer<TValue>.Default.Equals(Value, value))
            {
                lastValue = value;
                await ValueChanged.InvokeAsync(value);
            }
        }
        finally
        {
            notifying = false;
        }
    }

    /// <summary>
    /// Settles typed text when the user leaves the field or presses Enter on a closed list:
    /// an exact match selects that item, else custom text is kept when allowed, else the
    /// highlighted match is taken; anything else reverts to the selection.
    /// </summary>
    private async Task CommitTypedTextAsync()
    {
        if (Multiple)
        {
            // Typed text only filters the list; leaving discards it.
            inputText = "";
            Close();
            return;
        }

        if (!isFiltering && inputText == DisplayText)
        {
            Close();
            return;
        }

        var text = inputText.Trim();
        if (text.Length == 0)
        {
            await ClearAsync();
            return;
        }

        var exact = source.FindIndex(i => string.Equals(TextOf(i), text, StringComparison.OrdinalIgnoreCase));
        if (exact >= 0)
        {
            await SelectAsync(source[exact]);
            return;
        }

        if (AllowCustomValue)
        {
            await CommitCustomAsync(text);
            return;
        }

        if (HasActiveItem && IsItemRow(rows[activeRow]))
        {
            await SelectAsync(rows[activeRow].Item);
            return;
        }

        inputText = DisplayText;
        Close();
    }
}
