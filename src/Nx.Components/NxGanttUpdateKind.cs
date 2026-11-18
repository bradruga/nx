namespace Nx.Components;

/// <summary>What the user did to raise <see cref="NxGantt{TItem}.OnUpdate"/>.</summary>
public enum NxGanttUpdateKind
{
    /// <summary>A cell was edited in place; its editor already wrote the item.</summary>
    Edit,

    /// <summary>The bar was dragged (or moved with Alt+Left/Right): start and end move together.</summary>
    Move,

    /// <summary>The bar's left edge was dragged: a new start.</summary>
    ResizeStart,

    /// <summary>The bar's right edge was dragged (or Alt+Shift+Left/Right): a new end.</summary>
    ResizeEnd
}
