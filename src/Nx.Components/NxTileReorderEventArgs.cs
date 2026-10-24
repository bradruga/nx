namespace Nx.Components;

/// <summary>A tile an <see cref="NxTileLayout"/>'s user moved (<c>OnReorder</c>).</summary>
public sealed class NxTileReorderEventArgs
{
    internal NxTileReorderEventArgs(string id, int oldIndex, int newIndex)
    {
        Id = id;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The tile's <c>Id</c>.</summary>
    public string Id { get; }

    /// <summary>Its place before, from <c>0</c>.</summary>
    public int OldIndex { get; }

    /// <summary>Its place now, from <c>0</c>.</summary>
    public int NewIndex { get; }
}
