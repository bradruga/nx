namespace Nx.Components;

public partial class NxComboBox<TItem, TValue>
{
    /// <summary>
    /// One selected entry. <see cref="HasItem"/> is <c>false</c> for a bound value whose item is not
    /// loaded yet (a provider's list, or Data that arrives later); its chip shows the value.
    /// </summary>
    private readonly record struct Chip(TValue? Value, TItem? Item, bool HasItem);

    private List<Chip> chips = [];
    private List<Chip>? staged;                         // the overlay's working copy, committed by Done
    private IEnumerable<TValue>? lastValues;            // the Values parameter as last seen
    private IEnumerable<TItem>? lastSelectedItems;      // the SelectedItems parameter as last seen

    /// <summary>The selection being shown: the overlay's working copy while it is open, else the chips.</summary>
    private List<Chip> CurrentChips => staged ?? chips;

    private IEnumerable<Chip> VisibleChips => MaxVisibleChips is int max ? chips.Take(max) : chips;
    private int HiddenChipCount => MaxVisibleChips is int max ? Math.Max(0, chips.Count - max) : 0;

    private string ChipText(Chip chip) => chip.HasItem ? TextOf(chip.Item!) : chip.Value?.ToString() ?? "";

    private bool IsChosen(TItem item)
    {
        var value = ValueOf(item);
        foreach (var chip in CurrentChips)
        {
            if (chip.HasItem
                    ? EqualityComparer<TItem>.Default.Equals(chip.Item, item)
                    : EqualityComparer<TValue>.Default.Equals(chip.Value, value))
                return true;
        }
        return false;
    }

    private void SyncMultipleFromParameters(bool dataChanged)
    {
        var itemsChanged = initialized ? !ReferenceEquals(SelectedItems, lastSelectedItems) : SelectedItems is not null;
        var valuesChanged = !initialized || !ReferenceEquals(Values, lastValues);
        lastSelectedItems = SelectedItems;
        lastValues = Values;
        var before = chips;

        if (itemsChanged && SelectedItems is not null)
            chips = SelectedItems.Select(i => new Chip(ValueOf(i), i, true)).ToList();
        else if (valuesChanged)
            chips = ResolveValues(Values ?? []);
        else if (itemsChanged)
            chips = [];
        else if (dataChanged && chips.Any(c => !c.HasItem))
            chips = ResolveValues(chips.Select(c => c.Value!));

        // As with single select: setting one of Values and SelectedItems from code brings the other along.
        syncPending = !ReferenceEquals(before, chips) && (
            (ValuesChanged.HasDelegate && !(Values ?? []).SequenceEqual(ChipValues())) ||
            (SelectedItemsChanged.HasDelegate && !(SelectedItems ?? []).SequenceEqual(ChipItems())));
    }

    private List<Chip> ResolveValues(IEnumerable<TValue> values)
    {
        var resolved = new List<Chip>();
        foreach (var value in values)
        {
            var index = source.FindIndex(i => EqualityComparer<TValue>.Default.Equals(ValueOf(i), value));
            if (index >= 0)
            {
                resolved.Add(new Chip(value, source[index], true));
                continue;
            }
            var known = chips.FindIndex(c => c.HasItem && EqualityComparer<TValue>.Default.Equals(c.Value, value));
            resolved.Add(known >= 0 ? chips[known] : new Chip(value, default, false));
        }
        return resolved;
    }

    private List<TValue> ChipValues() => chips.Select(c => c.Value!).ToList();
    private List<TItem> ChipItems() => chips.Where(c => c.HasItem).Select(c => c.Item!).ToList();

    /// <summary>Adds or removes an item. In the overlay the change waits for Done.</summary>
    private async Task ToggleAsync(TItem item)
    {
        var target = staged ?? [.. chips];
        var value = ValueOf(item);
        var index = target.FindIndex(c => c.HasItem
            ? EqualityComparer<TItem>.Default.Equals(c.Item, item)
            : EqualityComparer<TValue>.Default.Equals(c.Value, value));
        if (index >= 0)
            target.RemoveAt(index);
        else if (MaxSelections is int max && target.Count >= max)
            return;
        else
            target.Add(new Chip(value, item, true));

        if (staged is not null)
        {
            staged = target;
            return;
        }
        chips = target;
        await NotifyMultipleAsync();
    }

    private async Task RemoveChipAsync(int index)
    {
        if (!Editable || index < 0 || index >= chips.Count)
            return;
        chips = [.. chips];
        chips.RemoveAt(index);
        await NotifyMultipleAsync();
    }

    private async Task ClearChipsAsync()
    {
        inputText = "";
        Close();
        if (chips.Count == 0)
            return;
        chips = [];
        await NotifyMultipleAsync();
    }

    private async Task NotifyMultipleAsync()
    {
        var values = ChipValues();
        var items = ChipItems();
        if (ValuesChanged.HasDelegate)
            lastValues = values;
        if (SelectedItemsChanged.HasDelegate)
            lastSelectedItems = items;
        notifying = true;
        try
        {
            await ValuesChanged.InvokeAsync(values);
            await SelectedItemsChanged.InvokeAsync(items);
        }
        finally
        {
            notifying = false;
        }
        validation.NotifyFieldChanged();
    }

    private async Task SyncMultipleBindingsAsync()
    {
        var values = ChipValues();
        var items = ChipItems();
        notifying = true;
        try
        {
            if (ValuesChanged.HasDelegate && !(Values ?? []).SequenceEqual(values))
            {
                lastValues = values;
                await ValuesChanged.InvokeAsync(values);
            }
            if (SelectedItemsChanged.HasDelegate && !(SelectedItems ?? []).SequenceEqual(items))
            {
                lastSelectedItems = items;
                await SelectedItemsChanged.InvokeAsync(items);
            }
        }
        finally
        {
            notifying = false;
        }
    }
}
