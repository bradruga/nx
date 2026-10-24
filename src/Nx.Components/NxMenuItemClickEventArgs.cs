namespace Nx.Components;

/// <summary>Raised when the user picks a menu item.</summary>
public sealed class NxMenuItemClickEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NxMenuItemClickEventArgs(NxMenuItem item, string? context)
    {
        Item = item;
        Context = context;
    }

    /// <summary>The item picked.</summary>
    public NxMenuItem Item { get; }

    /// <summary>For a context menu, the <c>data-nx-context</c> value of the element right-clicked (or its nearest ancestor with one).</summary>
    public string? Context { get; }
}
