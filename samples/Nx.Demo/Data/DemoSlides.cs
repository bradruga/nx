namespace Nx.Demo.Data;

/// <summary>A project picture for the carousel demos: a drawn scene stands in for a photo.</summary>
public sealed record DemoSlide(string Project, string Caption, string From, string To);

/// <summary>The carousel demos' slides.</summary>
public static class DemoSlides
{
    /// <summary>Five projects with a caption and two colors each.</summary>
    public static IReadOnlyList<DemoSlide> Projects { get; } =
    [
        new("Riverside Garage", "Level 3 double tees set", "#1f4e79", "#4f8fc4"),
        new("Harbor Lofts", "Podium columns poured", "#5b3a29", "#c08552"),
        new("North Yard", "Wall panels curing", "#2f5233", "#6fa36f"),
        new("Route 9 Bridge", "Box beams shipped", "#4a3d6b", "#9a87c9"),
        new("St. Anne's School", "Architectural spandrels", "#6b2e3a", "#cf6f7f"),
    ];
}
