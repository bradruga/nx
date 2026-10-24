using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// What NxDropDown, NxMultiSelect and NxAutoComplete switch on in the NxComboBox they render, cascaded under
/// <see cref="CascadeName"/>. Internal so the combo box's public API stays as it is; a combo box
/// without it behaves exactly as before. See NxComboBox.Modes.cs.
/// </summary>
internal sealed record ComboMode
{
    public const string CascadeName = "NxComboMode";

    /// <summary>No typing: the field opens the list, and letters jump to items (NxDropDown, NxMultiSelect without a filter).</summary>
    public bool PickOnly { get; init; }

    /// <summary>A first row that clears the selection, and the text shown while nothing is selected (NxDropDown).</summary>
    public string? DefaultText { get; init; }

    /// <summary>A first row that selects or clears every listed item (NxMultiSelect).</summary>
    public bool SelectAll { get; init; }

    /// <summary>The most items that can be selected (NxMultiSelect).</summary>
    public int? MaxSelections { get; init; }

    /// <summary>Above this many selected items the field shows <see cref="SummaryFormat"/> instead of chips; 0 = always (NxMultiSelect).</summary>
    public int? SummaryThreshold { get; init; }

    /// <summary>The summary text, with <c>{0}</c> for the count.</summary>
    public string SummaryFormat { get; init; } = "{0} selected";

    /// <summary>Free text with suggestions (NxAutoComplete): typing opens matches only, there is no arrow button, Escape keeps the text.</summary>
    public bool Suggest { get; init; }

    /// <summary>With <see cref="Suggest"/>, characters to type before suggestions show (at least 1).</summary>
    public int MinLength { get; init; } = 1;

    /// <summary>Content in the field before the text, e.g. a search icon (NxAutoComplete).</summary>
    public RenderFragment? Prefix { get; init; }
}
