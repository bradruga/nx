namespace Nx.Components;

/// <summary>Width preset of an <see cref="NxWindow"/>. <c>Width</c> and <c>Height</c> override it.</summary>
public enum NxWindowSize
{
    /// <summary>As wide as the content needs, up to the viewport.</summary>
    Auto,

    /// <summary>400 px. For confirmations.</summary>
    Small,

    /// <summary>600 px. Default; for short forms.</summary>
    Medium,

    /// <summary>900 px. For grids and long forms.</summary>
    Large
}
