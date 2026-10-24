using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Nx.Components;

// The modes NxDropDown, NxMultiSelect and NxAutoComplete switch on through a cascaded ComboMode: pick-only with
// type-ahead, the default ("none") row, select-all, a selection limit, the summary text, and suggestions.
// With no ComboMode every member here is inert.
public partial class NxComboBox<TItem, TValue>
{
    private const int TypeAheadResetMs = 700;

    [CascadingParameter(Name = ComboMode.CascadeName)] private ComboMode? Mode { get; set; }

    private string typeAhead = "";
    private DateTime typeAheadAt;

    /// <summary>A list row that is not a data item.</summary>
    private enum SpecialRow { None, Default, SelectAll }

    private bool PickOnly => Mode?.PickOnly == true;

    /// <summary>NxAutoComplete: the text is the value (custom values on) and the list only suggests.</summary>
    private bool Suggest => !Multiple && Mode?.Suggest == true;

    /// <summary>Enough is typed for suggestions.</summary>
    private bool CanSuggest => FilterText.Trim().Length >= Math.Max(1, Mode?.MinLength ?? 1);

    /// <summary>A suggestion list with nothing to suggest closes rather than saying "No matches".</summary>
    private void CloseIfNoSuggestions()
    {
        if (Suggest && rows.Count == 0)
            Close();
    }
    private string? DefaultText => Multiple ? null : Mode?.DefaultText;
    private bool HasSelectAll => Multiple && Mode?.SelectAll == true;
    private int? MaxSelections => Multiple ? Mode?.MaxSelections : null;

    private bool AtMaxSelections => MaxSelections is int max && CurrentChips.Count >= max;

    /// <summary>The field shows "3 selected" instead of chips.</summary>
    private bool ShowSummary => Multiple && Mode?.SummaryThreshold is int threshold && chips.Count > 0 && chips.Count > threshold;

    private string SummaryText => string.Format(CultureInfo.CurrentCulture, Mode?.SummaryFormat ?? "{0} selected", chips.Count);

    /// <summary>An unselected item that the limit keeps out.</summary>
    private bool BlockedByMax(TItem item) => AtMaxSelections && !IsSelected(item);

    /// <summary>Puts the default or select-all row first, renumbering the rows after it.</summary>
    private List<Row> WithSpecialRows(List<Row> built)
    {
        var special = DefaultText is not null ? SpecialRow.Default
                    : HasSelectAll && built.Any(r => !r.IsHeader) ? SpecialRow.SelectAll
                    : SpecialRow.None;
        if (special == SpecialRow.None)
            return built;
        var result = new List<Row>(built.Count + 1) { new(false, null, default!, -1, 0, special) };
        result.AddRange(built.Select(r => r with { RowIndex = r.RowIndex + 1 }));
        return result;
    }

    /// <summary>A row that holds a data item: not a group header, not a default or select-all row.</summary>
    private static bool IsItemRow(Row row) => !row.IsHeader && row.Special == SpecialRow.None;

    /// <summary>Items listed now (filtered), without headers or special rows.</summary>
    private IEnumerable<TItem> ListedItems => rows.Where(IsItemRow).Select(r => r.Item);

    private bool AllListedSelected => ListedItems.Any() && ListedItems.All(IsChosen);

    /// <summary>A click, Enter or Space on a row: the special rows do their own thing, items as before.</summary>
    private Task ActivateRowAsync(Row row) => row.Special switch
    {
        SpecialRow.Default   => PickDefaultAsync(),
        SpecialRow.SelectAll => ToggleAllAsync(),
        _                    => HandleOptionClick(row.Item)
    };

    private async Task PickDefaultAsync()
    {
        var fromOverlay = overlayOpen;
        await ClearAsync();
        if (fromOverlay)
            returnFocusPending = true;
    }

    /// <summary>Selects every listed item (up to the limit), or clears them all when they already are.</summary>
    private async Task ToggleAllAsync()
    {
        var target = staged ?? [.. chips];
        if (AllListedSelected)
        {
            var listed = ListedItems.ToList();
            target.RemoveAll(c => c.HasItem && listed.Any(i => EqualityComparer<TItem>.Default.Equals(i, c.Item)));
        }
        else
        {
            foreach (var item in ListedItems)
            {
                if (MaxSelections is int max && target.Count >= max)
                    break;
                var value = ValueOf(item);
                if (!target.Any(c => c.HasItem ? EqualityComparer<TItem>.Default.Equals(c.Item, item) : EqualityComparer<TValue>.Default.Equals(c.Value, value)))
                    target.Add(new Chip(value, item, true));
            }
        }
        if (staged is not null)
        {
            staged = target;
            return;
        }
        chips = target;
        await NotifyMultipleAsync();
    }

    /// <summary>
    /// Pick-only keys on the field. Closed: Enter, Space and the arrows open the list; letters select the next
    /// item that starts with them, as a native select does. Open: Space picks too, Home and End jump, letters
    /// move the highlight. Returns <c>true</c> when handled.
    /// </summary>
    private async Task<bool> HandlePickKeyAsync(KeyboardEventArgs e)
    {
        if (e.CtrlKey || e.MetaKey)
            return false;
        if (!IsTypeAheadKey(e))
            typeAhead = "";      // a prefix is letters typed in a row; any other key ends it
        if (!isOpen)
        {
            if (e.Key is "Enter" or " " or "ArrowDown" or "ArrowUp")
            {
                await OpenAsync(filtering: false);
                return true;
            }
            if (IsTypeAheadKey(e))
            {
                if (TypeAheadMatch(e.Key, startAfter: CurrentItemRow()) is { } item)
                {
                    if (Multiple)
                        await ToggleAsync(item);
                    else
                        await SelectAsync(item);
                }
                return true;
            }
            return false;
        }
        switch (e.Key)
        {
            case " ":
            case "Enter":
                if (HasActiveItem)
                {
                    var row = rows[activeRow];
                    await ActivateRowAsync(row);
                }
                return true;
            case "Home":
                activeRow = rows.FindIndex(r => !r.IsHeader);
                navigated = true;
                scrollPending = true;
                return true;
            case "End":
                activeRow = rows.FindLastIndex(r => !r.IsHeader);
                navigated = true;
                scrollPending = true;
                return true;
            case "Tab":
                inputText = DisplayText;
                Close();
                return false;
        }
        if (IsTypeAheadKey(e))
        {
            var match = TypeAheadRow(e.Key, startAfter: activeRow);
            if (match >= 0)
            {
                activeRow = match;
                navigated = true;
                scrollPending = true;
            }
            return true;
        }
        return false;
    }

    private static bool IsTypeAheadKey(KeyboardEventArgs e) => e.Key.Length == 1 && e.Key != " " && !e.AltKey;

    /// <summary>The row of the selected item in the full list, for type-ahead on a closed field.</summary>
    private int CurrentItemRow()
    {
        if (rows.Count == 0)
            BuildRows();
        return hasSelection ? rows.FindIndex(r => IsItemRow(r) && IsSelected(r.Item)) : -1;
    }

    private TItem? TypeAheadMatch(string key, int startAfter)
    {
        if (rows.Count == 0)
            BuildRows();
        var row = TypeAheadRow(key, startAfter);
        return row >= 0 ? rows[row].Item : default;
    }

    /// <summary>
    /// Keys typed within <see cref="TypeAheadResetMs"/> build a prefix. The same letter pressed again cycles
    /// through the items that start with it; a longer prefix finds the first match from the current row.
    /// </summary>
    private int TypeAheadRow(string key, int startAfter)
    {
        var now = DateTime.UtcNow;
        if ((now - typeAheadAt).TotalMilliseconds > TypeAheadResetMs)
            typeAhead = "";
        typeAheadAt = now;
        typeAhead += key;

        var cycling = typeAhead.Length > 1 && typeAhead.All(c => char.ToLowerInvariant(c) == char.ToLowerInvariant(typeAhead[0]));
        var prefix = cycling ? typeAhead[..1] : typeAhead;
        // A new or cycling prefix searches after the current row; a growing one may keep the current row.
        var from = typeAhead.Length == 1 || cycling ? startAfter + 1 : Math.Max(startAfter, 0);
        for (var i = 0; i < rows.Count; i++)
        {
            var index = (from + i) % rows.Count;
            var row = rows[index];
            if (!IsItemRow(row))
                continue;
            if (TextOf(row.Item).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                return index;
        }
        return -1;
    }
}
