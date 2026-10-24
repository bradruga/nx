namespace Nx.Components;

/// <summary>An <see cref="NxTileLayout"/>'s arrangement: each tile's place and size, to save and restore.</summary>
public sealed class NxTileLayoutState
{
    /// <summary>The tiles, in order.</summary>
    public List<NxTileLayoutItemState> ItemStates { get; set; } = [];
}
