namespace Nx.Components;

/// <summary>Color of an <see cref="NxIcon"/>, from the theme's tokens.</summary>
public enum NxIconColor
{
    /// <summary>The surrounding text color. Default.</summary>
    Inherit,

    /// <summary><c>--nx-accent</c>.</summary>
    Primary,

    /// <summary><c>--nx-success</c>.</summary>
    Success,

    /// <summary><c>--nx-warning</c>.</summary>
    Warning,

    /// <summary><c>--nx-danger</c>.</summary>
    Danger,

    /// <summary><c>--nx-fg-muted</c>.</summary>
    Muted
}
