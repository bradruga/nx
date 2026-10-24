namespace Nx.Components;

/// <summary>
/// An icon's drawing: the inside of an <c>&lt;svg&gt;</c>. The built-in ones are on <see cref="NxIcons"/>; make your
/// own with the markup of any 24×24 stroked icon, or set <see cref="Filled"/> and <see cref="ViewBox"/> for others.
/// </summary>
/// <param name="Name">A short name, rendered as <c>data-icon</c>.</param>
/// <param name="Content">The SVG elements inside the <c>&lt;svg&gt;</c>.</param>
public sealed record NxIconData(string Name, string Content)
{
    /// <summary>The coordinate system of <see cref="Content"/>. Default: <c>0 0 24 24</c>.</summary>
    public string ViewBox { get; init; } = "0 0 24 24";

    /// <summary>Paints the shapes with <c>fill</c> instead of a 2-unit stroke.</summary>
    public bool Filled { get; init; }
}
