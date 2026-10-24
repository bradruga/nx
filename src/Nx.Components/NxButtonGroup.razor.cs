using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// Joins <see cref="NxButton"/>s (and <see cref="NxDropDownButton"/>s) into one bar of related actions — Today ‹ ›,
/// Undo Redo. <see cref="Variant"/>, <see cref="Size"/> and <see cref="Disabled"/> apply to every button that does
/// not set its own. To pick one value from a set of joined buttons, use <see cref="NxToggleGroup{TValue}"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxButtonGroup AriaLabel="Week" Size="NxButtonSize.Small"&gt;
///     &lt;NxButton OnClick="GoToToday"&gt;Today&lt;/NxButton&gt;
///     &lt;NxButton OnClick="PreviousWeek" AriaLabel="Previous week" Icon="…" /&gt;
///     &lt;NxButton OnClick="NextWeek" AriaLabel="Next week" Icon="…" /&gt;
/// &lt;/NxButtonGroup&gt;
/// </code>
/// </example>
public partial class NxButtonGroup
{
    /// <summary>The buttons.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Visual style of every button that does not set its own. Default: each button's default.</summary>
    [Parameter] public NxButtonVariant? Variant { get; set; }

    /// <summary>Size of every button that does not set its own. Default: each button's default.</summary>
    [Parameter] public NxButtonSize? Size { get; set; }

    /// <summary>Disables every button in the group.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Stretches the group to its container's width, the buttons sharing it equally.</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>The group's accessible name (<c>role="group"</c>).</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Extra CSS class on the group.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the group.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the group element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-buttongroup",
        FullWidth ? "nx-buttongroup-full-width" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));
}
