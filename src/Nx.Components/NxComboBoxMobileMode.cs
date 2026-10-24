namespace Nx.Components;

/// <summary>
/// When an <c>NxComboBox</c> opens a full-screen overlay instead of a popup. Set via <c>MobileMode</c>.
/// </summary>
public enum NxComboBoxMobileMode
{
    /// <summary>The overlay below <c>MobileBreakpoint</c> pixels of viewport width, the popup above. Default.</summary>
    Auto,

    /// <summary>Always the overlay.</summary>
    Always,

    /// <summary>Always the popup.</summary>
    Never
}
