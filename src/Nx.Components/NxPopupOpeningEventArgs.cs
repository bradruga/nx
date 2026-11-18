namespace Nx.Components;

/// <summary>Raised by an <see cref="NxPopup"/> with <c>OpenOnHover</c> before it opens for an anchor: set the content for that anchor, or cancel.</summary>
public sealed class NxPopupOpeningEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NxPopupOpeningEventArgs(string? context) => Context = context;

    /// <summary>The anchor's <c>data-nx-context</c> (or its nearest ancestor's), as NxContextMenu gives; <c>null</c> without one.</summary>
    public string? Context { get; }

    /// <summary>Set to <c>true</c> to keep the popup closed.</summary>
    public bool Cancel { get; set; }
}
