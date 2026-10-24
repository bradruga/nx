namespace Nx.Components;

/// <summary>Which side of its anchor an <see cref="NxTooltip"/> prefers. It flips to the opposite side when there is no room.</summary>
public enum NxTooltipPlacement
{
    /// <summary>Above. Default.</summary>
    Top,

    /// <summary>Below.</summary>
    Bottom,

    /// <summary>To the left.</summary>
    Left,

    /// <summary>To the right.</summary>
    Right
}
