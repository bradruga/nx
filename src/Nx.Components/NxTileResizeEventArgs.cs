namespace Nx.Components;

/// <summary>A tile an <see cref="NxTileLayout"/>'s user resized (<c>OnResize</c>).</summary>
public sealed class NxTileResizeEventArgs
{
    internal NxTileResizeEventArgs(string id, int colSpan, int rowSpan)
    {
        Id = id;
        ColSpan = colSpan;
        RowSpan = rowSpan;
    }

    /// <summary>The tile's <c>Id</c>.</summary>
    public string Id { get; }

    /// <summary>Columns it spans now.</summary>
    public int ColSpan { get; }

    /// <summary>Rows it spans now.</summary>
    public int RowSpan { get; }
}
