namespace Nx.Components;

/// <summary>Scoped (one per circuit): where an <see cref="NxWindow"/> finds the layout's <see cref="NxWindowHost"/>.</summary>
internal sealed class NxWindowHostState
{
    /// <summary>The host in the layout, if any.</summary>
    public NxWindowHost? Host { get; set; }
}
