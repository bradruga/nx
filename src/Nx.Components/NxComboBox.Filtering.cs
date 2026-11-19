namespace Nx.Components;

public partial class NxComboBox<TItem, TValue>
{
    private async Task OpenAsync(bool filtering)
    {
        await ViewportKnownAsync();
        if (!Editable)
            return;
        if (UseOverlay)
        {
            await OpenOverlayAsync();
            return;
        }
        isFiltering = filtering;
        if (!isOpen)
        {
            isOpen = true;
            popupPlaced = false;
        }
        await RefreshListAsync(debounce: filtering);
    }

    /// <summary>Rebuilds the rows for the current filter text: from <see cref="Data"/> at once, or from the provider.</summary>
    private async Task RefreshListAsync(bool debounce)
    {
        if (DataProvider is not null)
        {
            // Opening without typing shows the unfiltered list.
            await LoadAsync(isFiltering ? FilterText : "", debounce);
        }
        else
        {
            BuildRows();
            SetInitialActive();
            CloseIfNoSuggestions();
        }
    }

    private void Close()
    {
        if (!isOpen)
            return;
        if (overlayOpen)
            CloseOverlayState();
        isOpen = false;
        measureFailed = false;
        isFiltering = false;
        activeRow = -1;
        closePending = true;
        providerCts?.Cancel();
        providerLoading = false;
    }

    private void SetInitialActive()
    {
        activeRow = -1;
        navigated = false;
        // Suggestions highlight nothing until the user arrows to one, so Enter keeps the typed text.
        if (Suggest)
            return;
        var text = FilterText.Trim();
        if (isFiltering && text.Length > 0)
        {
            // With Filter None the list stays whole, so the highlight goes to the first match instead.
            if (DataProvider is null && CustomFilter is null && Filter == NxComboBoxFilter.None)
                activeRow = rows.FindIndex(r => IsItemRow(r) && TextOf(r.Item).StartsWith(text, StringComparison.OrdinalIgnoreCase));
            if (activeRow < 0)
                activeRow = rows.FindIndex(r => !r.IsHeader);
        }
        else
        {
            if (Multiple ? chips.Count > 0 : hasSelection)
                activeRow = rows.FindIndex(r => IsItemRow(r) && IsSelected(r.Item));
            if (activeRow < 0)
                activeRow = rows.FindIndex(r => !r.IsHeader);
        }
        scrollPending = true;
    }

    private bool Matches(TItem item, string text, Func<string, string, bool> test)
    {
        if (test(TextOf(item), text))
            return true;
        if (!FilterAllColumns)
            return false;
        foreach (var column in EffectiveColumns)
        {
            if (column.Field is not null && test(column.TextOf(item), text))
                return true;
        }
        return false;
    }

    private void BuildRows()
    {
        IEnumerable<TItem> items = source;
        var text = isFiltering ? FilterText : "";

        if (DataProvider is null && text.Length > 0)
        {
            if (CustomFilter is not null)
                items = items.Where(i => CustomFilter(i, text));
            else if (Filter == NxComboBoxFilter.Contains)
                items = items.Where(i => Matches(i, text, (s, t) => s.Contains(t, StringComparison.OrdinalIgnoreCase)));
            else if (Filter == NxComboBoxFilter.StartsWith)
                items = items.Where(i => Matches(i, text, (s, t) => s.StartsWith(t, StringComparison.OrdinalIgnoreCase)));
        }

        var built = new List<Row>();
        var index = 0;
        if (ItemGroup is null)
        {
            foreach (var item in items)
                built.Add(new Row(false, null, item, index++, built.Count));
        }
        else
        {
            foreach (var group in items.GroupBy(i => ItemGroup(i) ?? ""))
            {
                built.Add(new Row(true, group.Key, default!, -1, built.Count));
                foreach (var item in group)
                    built.Add(new Row(false, group.Key, item, index++, built.Count));
            }
        }
        rows = WithSpecialRows(built);
        itemCount = index;
    }

    // A Value set before the provider's list had loaded gets its item (and the field its text) now.
    private void SelectLoadedValue()
    {
        if (Multiple || hasSelection || customText is not null || EqualityComparer<TValue>.Default.Equals(Value, default))
            return;
        foreach (var item in source)
        {
            if (!EqualityComparer<TValue>.Default.Equals(ValueOf(item), Value))
                continue;
            SetSelection(item);
            if (!isFiltering)
                inputText = DisplayText;
            syncPending = SelectedItemChanged.HasDelegate && !EqualityComparer<TItem>.Default.Equals(SelectedItem, item);
            return;
        }
    }

    private async Task LoadAsync(string filter, bool debounce)
    {
        providerCts?.Cancel();
        var cts = providerCts = new CancellationTokenSource();
        providerLoading = true;
        StateHasChanged();

        try
        {
            if (debounce && DebounceDelay > 0)
                await Task.Delay(DebounceDelay, cts.Token);
            var items = await DataProvider!(new NxComboBoxRequest(filter, cts.Token));
            if (cts.IsCancellationRequested)
                return;
            source = items.ToList();
            SelectLoadedValue();
        }
        catch (OperationCanceledException)
        {
            return;
        }

        providerLoading = false;
        if (!isOpen)
            return;
        BuildRows();
        SetInitialActive();
        CloseIfNoSuggestions();
    }
}
