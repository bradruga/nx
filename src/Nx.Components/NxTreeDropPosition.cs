namespace Nx.Components;

/// <summary>Where a dragged item was dropped, relative to the item under it.</summary>
public enum NxTreeDropPosition
{
    /// <summary>Just above it, as its sibling.</summary>
    Before,

    /// <summary>Onto it, as its child.</summary>
    Inside,

    /// <summary>Just below it, as its sibling.</summary>
    After
}
