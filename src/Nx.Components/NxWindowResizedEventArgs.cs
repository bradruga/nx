namespace Nx.Components;

/// <summary>Raised by <see cref="NxWindow"/> when the user resized it: its new size, in px.</summary>
/// <param name="Width">Width in px.</param>
/// <param name="Height">Height in px.</param>
public sealed record NxWindowResizedEventArgs(int Width, int Height);
