namespace Nx.Components;

/// <summary>
/// When a picker (<see cref="NxDatePicker{TValue}"/>, <see cref="NxDateRangePicker{TValue}"/>, <see cref="NxTimePicker{TValue}"/>)
/// or a combo box (<c>NxComboBox</c>, <c>NxDropDown</c>, <c>NxMultiSelect</c>) opens a full-screen sheet instead of a popup.
/// Set via <c>MobileMode</c>.
/// </summary>
public enum NxMobileMode
{
    /// <summary>The sheet at or below <c>MobileBreakpoint</c> pixels of viewport width, the popup above. The pickers' default.</summary>
    Auto,

    /// <summary>Always the sheet.</summary>
    Always,

    /// <summary>Always the popup. The combo boxes' default.</summary>
    Never
}
