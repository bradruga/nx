namespace Nx.Components;

/// <summary>
/// How typed text filters an <c>NxComboBox</c> list. Set via <c>Filter</c>; ignored when
/// <c>CustomFilter</c> is set or the list comes from a <c>DataProvider</c>.
/// </summary>
public enum NxComboBoxFilter
{
    /// <summary>Items whose text contains the typed text, ignoring case. Default.</summary>
    Contains,

    /// <summary>Items whose text starts with the typed text, ignoring case.</summary>
    StartsWith,

    /// <summary>No filtering; typing only moves the highlight to the first match.</summary>
    None
}
