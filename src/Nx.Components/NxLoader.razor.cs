using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// An indeterminate busy indicator: a spinner, converging dots or pulsing dots in three sizes, drawn in
/// <c>--nx-loader-color</c>. Announced as a progress bar named <see cref="AriaLabel"/>. To cover an area while it
/// loads, use <see cref="NxLoaderContainer"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxLoader /&gt;
/// &lt;NxLoader Type="NxLoaderType.Pulsing" Size="NxSize.Small" /&gt;
/// </code>
/// </example>
public partial class NxLoader
{
    /// <summary>The animation. Default: <see cref="NxLoaderType.Spinner"/>.</summary>
    [Parameter] public NxLoaderType Type { get; set; }

    /// <summary>Size: 16, 28 or 44 px by default. Default: <see cref="NxSize.Medium"/>.</summary>
    [Parameter] public NxSize Size { get; set; } = NxSize.Medium;

    /// <summary>The accessible name. Default: "Loading".</summary>
    [Parameter] public string AriaLabel { get; set; } = "Loading";

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element, e.g. <c>--nx-loader-color</c>.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-loader",
        Type switch
        {
            NxLoaderType.Converging => "nx-loader-converging",
            NxLoaderType.Pulsing    => "nx-loader-pulsing",
            _                       => "nx-loader-spinner"
        },
        Size switch
        {
            NxSize.Small => "nx-loader-sm",
            NxSize.Large => "nx-loader-lg",
            _            => "nx-loader-md"
        },
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));
}
