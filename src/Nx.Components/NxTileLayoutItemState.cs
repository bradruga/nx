namespace Nx.Components;

/// <summary>One tile in an <see cref="NxTileLayoutState"/>.</summary>
public sealed class NxTileLayoutItemState
{
    /// <summary>The tile's <c>Id</c>.</summary>
    public string Id { get; set; } = "";

    /// <summary>Its place, from <c>0</c>.</summary>
    public int Order { get; set; }

    /// <summary>Columns it spans.</summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>Rows it spans.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Whether it shows.</summary>
    public bool Visible { get; set; } = true;
}
