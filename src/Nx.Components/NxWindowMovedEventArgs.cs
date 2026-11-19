namespace Nx.Components;

/// <summary>Raised by <see cref="NxWindow"/> when the user moved it: its new place in the viewport, in px.</summary>
/// <param name="Left">Distance from the viewport's left edge, in px.</param>
/// <param name="Top">Distance from the viewport's top edge, in px.</param>
public sealed record NxWindowMovedEventArgs(int Left, int Top);
