namespace Nx.Components;

/// <summary>How an <see cref="NxPopup"/> lines up with its anchor along the side it opens on; it shifts to stay on screen.</summary>
public enum NxPopupAlign
{
    /// <summary>Left edges (top edges for Left / Right placement) line up.</summary>
    Start,

    /// <summary>Centered on the anchor.</summary>
    Center,

    /// <summary>Right edges (bottom edges for Left / Right placement) line up.</summary>
    End
}
