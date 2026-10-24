namespace Nx.Components;

/// <summary>How many items of an <see cref="NxTreeView{TItem}"/> can be selected.</summary>
public enum NxTreeSelectionMode
{
    /// <summary>None; a click only moves the keyboard's place.</summary>
    None,

    /// <summary>One item. Default.</summary>
    Single,

    /// <summary>Any number; a click or Space toggles an item.</summary>
    Multiple
}
