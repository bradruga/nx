namespace Nx.Components;

/// <summary>A drag and drop in an <see cref="NxTreeView{TItem}"/>: move <see cref="Item"/> in your data, and the tree follows.</summary>
/// <typeparam name="TItem">The tree's item type.</typeparam>
public sealed class NxTreeDropEventArgs<TItem>
{
    internal NxTreeDropEventArgs(TItem item, TItem destination, NxTreeDropPosition position)
    {
        Item = item;
        Destination = destination;
        Position = position;
    }

    /// <summary>The item dragged.</summary>
    public TItem Item { get; }

    /// <summary>The item it was dropped on.</summary>
    public TItem Destination { get; }

    /// <summary>Before, onto (as a child) or after <see cref="Destination"/>.</summary>
    public NxTreeDropPosition Position { get; }
}
