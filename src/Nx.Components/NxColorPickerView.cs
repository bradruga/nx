namespace Nx.Components;

/// <summary>What an <see cref="NxColorPicker"/>'s popup offers.</summary>
public enum NxColorPickerView
{
    /// <summary>The grid of swatches.</summary>
    Palette,

    /// <summary>Any color: the <see cref="NxColorGradient"/> with an Apply button.</summary>
    Gradient,

    /// <summary>Both, on two tabs.</summary>
    Both
}
