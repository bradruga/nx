namespace Nx.Components;

/// <summary>Which side of an <see cref="NxTabStrip"/> its tabs sit on.</summary>
public enum NxTabPosition
{
    /// <summary>Above the content. Default.</summary>
    Top,

    /// <summary>Below the content.</summary>
    Bottom,

    /// <summary>A column left of the content; on phones the tabs move to the top.</summary>
    Left,

    /// <summary>A column right of the content; on phones the tabs move to the top.</summary>
    Right
}
