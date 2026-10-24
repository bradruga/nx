namespace Nx.Components;

/// <summary>
/// When a picker (<see cref="NxDatePicker{TValue}"/>, <see cref="NxDateRangePicker{TValue}"/>, <see cref="NxTimePicker{TValue}"/>)
/// opens a full-screen sheet instead of a popup. Set via <c>MobileMode</c>.
/// </summary>
public enum NxMobileMode
{
    /// <summary>The sheet at or below <c>MobileBreakpoint</c> pixels of viewport width, the popup above. Default.</summary>
    Auto,

    /// <summary>Always the sheet.</summary>
    Always,

    /// <summary>Always the popup.</summary>
    Never
}
