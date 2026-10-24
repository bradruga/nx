namespace Nx.Components;

/// <summary>How <see cref="NxColorGradient"/> writes a color, in its value and its text inputs.</summary>
public enum NxColorFormat
{
    /// <summary><c>#rrggbb</c>, or <c>#rrggbbaa</c> when not opaque.</summary>
    Hex,

    /// <summary><c>rgb(r, g, b)</c>, or <c>rgba(r, g, b, a)</c> when not opaque.</summary>
    Rgb
}
