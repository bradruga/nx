namespace Nx.Components;

/// <summary>Raised before an <see cref="NxContextMenu"/> opens. Change the items for this target, or cancel.</summary>
public sealed class NxContextMenuOpeningEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NxContextMenuOpeningEventArgs(double x, double y, string? context)
    {
        X = x;
        Y = y;
        Context = context;
    }

    /// <summary>Viewport x of the right click or long press.</summary>
    public double X { get; }

    /// <summary>Viewport y of the right click or long press.</summary>
    public double Y { get; }

    /// <summary>The <c>data-nx-context</c> value of the element pressed (or its nearest ancestor with one), e.g. a row id.</summary>
    public string? Context { get; }

    /// <summary>Keeps the menu closed (the browser's own menu does not show either).</summary>
    public bool Cancel { get; set; }
}
