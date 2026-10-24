using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// An inline SVG icon from <see cref="NxIcons"/> (or your own <see cref="NxIconData"/>), sized by a token and drawn in
/// the text color unless <see cref="Color"/> says otherwise. Decorative (<c>aria-hidden</c>) unless it has an
/// <see cref="AriaLabel"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxIcon Icon="NxIcons.Trash" /&gt;
/// &lt;NxIcon Icon="NxIcons.CheckCircle" Size="NxIconSize.Large" Color="NxIconColor.Success" AriaLabel="Approved" /&gt;
/// </code>
/// </example>
public partial class NxIcon
{
    /// <summary>The icon to draw.</summary>
    [Parameter, EditorRequired] public NxIconData Icon { get; set; } = NxIcons.Circle;

    /// <summary>Size. Default: <see cref="NxIconSize.Medium"/> (16 px).</summary>
    [Parameter] public NxIconSize Size { get; set; } = NxIconSize.Medium;

    /// <summary>Color. Default: <see cref="NxIconColor.Inherit"/>, the text color around it.</summary>
    [Parameter] public NxIconColor Color { get; set; }

    /// <summary>The accessible name of an icon that means something on its own; without it the icon is hidden from screen readers.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>A tooltip, rendered as the SVG's <c>&lt;title&gt;</c>.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Extra CSS class on the <c>&lt;svg&gt;</c>.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the <c>&lt;svg&gt;</c>.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>id</c>, <c>data-*</c>…) are passed to the <c>&lt;svg&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool Decorative => AriaLabel is null;

    private string CssClass => string.Join(' ', new[]
    {
        "nx-icon",
        Size switch
        {
            NxIconSize.XSmall   => "nx-icon-xs",
            NxIconSize.Small    => "nx-icon-sm",
            NxIconSize.Large    => "nx-icon-lg",
            NxIconSize.XLarge   => "nx-icon-xl",
            NxIconSize.XXLarge  => "nx-icon-2xl",
            NxIconSize.XXXLarge => "nx-icon-3xl",
            _                   => "nx-icon-md"
        },
        Color switch
        {
            NxIconColor.Primary => "nx-icon-primary",
            NxIconColor.Success => "nx-icon-success",
            NxIconColor.Warning => "nx-icon-warning",
            NxIconColor.Danger  => "nx-icon-danger",
            NxIconColor.Muted   => "nx-icon-muted",
            _                   => null
        },
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));
}
