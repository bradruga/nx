namespace Nx.Components;

/// <summary>Which way the user may drag an <see cref="NxTextArea"/>'s corner handle. Ignored with <c>AutoResize</c>.</summary>
public enum NxTextAreaResize
{
    /// <summary>No handle.</summary>
    None,

    /// <summary>Taller or shorter. Default.</summary>
    Vertical,

    /// <summary>Any direction.</summary>
    Both
}
