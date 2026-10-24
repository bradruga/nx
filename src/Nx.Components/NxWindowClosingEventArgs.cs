namespace Nx.Components;

/// <summary>Raised by <see cref="NxWindow"/> before the user closes it. Set <see cref="Cancel"/> to keep it open.</summary>
public sealed class NxWindowClosingEventArgs
{
    /// <summary>Keeps the window open, e.g. to ask about unsaved changes first.</summary>
    public bool Cancel { get; set; }
}
