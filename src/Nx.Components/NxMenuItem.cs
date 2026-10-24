using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// One entry of an <see cref="NxMenu"/>, <see cref="NxContextMenu"/>, <see cref="NxDropDownButton"/> or
/// <see cref="NxSplitButton"/>: an action, a check item, a submenu (<see cref="Items"/>) or a separator.
/// </summary>
public sealed class NxMenuItem
{
    /// <summary>Creates an item with this text.</summary>
    public NxMenuItem(string text) => Text = text;

    private NxMenuItem() => Text = "";

    /// <summary>A line between groups of items.</summary>
    public static NxMenuItem Separator() => new() { IsSeparator = true };

    /// <summary>The item's text, also matched by the first-letter keys.</summary>
    public string Text { get; }

    /// <summary>A key for your own use, e.g. to tell items apart in <c>OnItemClick</c>.</summary>
    public string? Id { get; init; }

    /// <summary>Icon before the text.</summary>
    public RenderFragment? Icon { get; init; }

    /// <summary>Shortcut text shown at the end, e.g. <c>"Ctrl+C"</c>. Display only: the menu does not bind the keys.</summary>
    public string? Shortcut { get; init; }

    /// <summary>Greyed out and skipped by the keyboard.</summary>
    public bool Disabled { get; init; }

    /// <summary><c>true</c> or <c>false</c> makes a check item with a check mark; <c>null</c> (default) a plain item.</summary>
    public bool? Checked { get; init; }

    /// <summary>A submenu: the item opens it instead of being clicked.</summary>
    public IReadOnlyList<NxMenuItem>? Items { get; init; }

    /// <summary>Any value to carry along to <c>OnItemClick</c>.</summary>
    public object? Data { get; init; }

    /// <summary>Whether this is a separator line.</summary>
    public bool IsSeparator { get; private init; }

    internal bool HasSubmenu => Items is { Count: > 0 };
}
