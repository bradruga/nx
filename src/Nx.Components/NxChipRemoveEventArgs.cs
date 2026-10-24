namespace Nx.Components;

/// <summary>A chip the user removed from an <see cref="NxChipList{TItem}"/>.</summary>
/// <typeparam name="TItem">The list's item type.</typeparam>
public sealed class NxChipRemoveEventArgs<TItem>
{
    internal NxChipRemoveEventArgs(TItem item) => Item = item;

    /// <summary>The item whose chip was removed; take it out of the list's data.</summary>
    public TItem Item { get; }
}
