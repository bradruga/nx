using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// Covers its nearest positioned ancestor (give the area <c>position: relative</c>) with a translucent overlay and a
/// centered <see cref="NxLoader"/> while <see cref="Visible"/>; the overlay catches clicks on what is underneath.
/// Recolor the overlay with <c>--nx-loader-overlay-bg</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;div style="position: relative"&gt;
///     &lt;NxLoaderContainer Visible="loading" Text="Loading orders…" /&gt;
///     …
/// &lt;/div&gt;
/// </code>
/// </example>
public partial class NxLoaderContainer
{
    /// <summary>Shows the overlay. Default: <c>true</c>.</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>Text under the loader. Default: none.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>The loader's animation. Default: <see cref="NxLoaderType.Spinner"/>.</summary>
    [Parameter] public NxLoaderType LoaderType { get; set; }

    /// <summary>The loader's size. Default: <see cref="NxSize.Large"/>.</summary>
    [Parameter] public NxSize LoaderSize { get; set; } = NxSize.Large;

    /// <summary>Replaces the loader and text in the middle of the overlay, e.g. a progress message.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Extra CSS class on the overlay.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the overlay, e.g. <c>--nx-loader-overlay-bg</c>.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the overlay.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-loader-container" : $"nx-loader-container {Class}";
}
