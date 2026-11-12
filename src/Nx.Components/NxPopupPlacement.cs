namespace Nx.Components;

/// <summary>Which side of its anchor an <see cref="NxPopup"/> opens on; it flips to the opposite side when there is no room.</summary>
public enum NxPopupPlacement
{
    /// <summary>Below the anchor.</summary>
    Bottom,

    /// <summary>Above the anchor.</summary>
    Top,

    /// <summary>Right of the anchor.</summary>
    Right,

    /// <summary>Left of the anchor.</summary>
    Left
}
