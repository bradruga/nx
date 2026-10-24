namespace Nx.Components;

/// <summary>Ready-made palettes for <see cref="NxColorPicker"/>.</summary>
public static class NxColorPalettes
{
    /// <summary>
    /// The Office palette, 10 columns: the ten theme colors, five rows of their tints and shades, and the ten standard
    /// colors — the palette ERP's color pickers use (the default).
    /// </summary>
    public static IReadOnlyList<string> Office { get; } =
    [
        "#ffffff", "#000000", "#e6e6e6", "#435569", "#4371c4", "#ed7e32", "#a5a4a5", "#febf04", "#5a9bd5", "#71ae48",
        "#f2f2f3", "#7f7f7f", "#d1cece", "#d5dde3", "#dae1f4", "#fce5d4", "#deeded", "#fff2cc", "#deeaf6", "#e1efd9",
        "#d7d8d8", "#585959", "#aeabab", "#adbaca", "#b4c5e7", "#f6caac", "#dbdbdb", "#ffe498", "#bcd6ee", "#c5e0b2",
        "#bfbfc0", "#3f3f3f", "#767070", "#8595b1", "#8fabdb", "#f5b183", "#c9c8c9", "#fed965", "#9bc4e5", "#a8d08d",
        "#a5a5a6", "#262625", "#393939", "#334050", "#2e5496", "#c45a11", "#7b7b7a", "#bf9000", "#2f75b5", "#548235",
        "#7f7f7f", "#0b0c0c", "#161616", "#222a34", "#203764", "#843d0b", "#525252", "#7f6000", "#1d4d79", "#375623",
        "#c00000", "#ff0000", "#ffc000", "#ffff00", "#92d050", "#00b050", "#00b0f0", "#0070c0", "#002060", "#7030a0",
    ];

    /// <summary>Twelve clear, distinct colors for marking categories, crews or loads on a schedule; 6 columns.</summary>
    public static IReadOnlyList<string> Basic { get; } =
    [
        "#c0392b", "#d97a1e", "#e1b12c", "#3e9e4f", "#1e8c8c", "#2e6db4",
        "#2d247a", "#7d3fbf", "#c0398a", "#8b5a2b", "#5f6b73", "#1f1f23",
    ];
}
