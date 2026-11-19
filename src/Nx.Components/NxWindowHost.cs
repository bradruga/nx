using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Nx.Components;

/// <summary>
/// Optional: draws every <see cref="NxWindow"/> (and <see cref="NxDialog"/>) of the page here instead of where it is
/// declared, so an ancestor with a CSS <c>transform</c>, <c>filter</c> or clipping cannot misplace or cut it. Put one in
/// the layout, after the page content, and register it with <c>services.AddNxDialogs()</c>. Without a host windows are
/// drawn in place; <see cref="NxWindow.Inline"/> keeps one window in place.
/// </summary>
/// <example>
/// <code>
/// @@Body
/// &lt;NxWindowHost /&gt;
/// </code>
/// </example>
public sealed class NxWindowHost : ComponentBase, IDisposable
{
    private readonly List<NxWindow> windows = [];
    private NxWindowHostState? state;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        state = Services.GetService<NxWindowHostState>()
            ?? throw new InvalidOperationException("NxWindowHost needs its service: call services.AddNxDialogs().");
        state.Host = this;
    }

    // In code, not Razor: the outlet is internal, and Razor only knows public components.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        foreach (var window in windows.Where(w => w.Visible))
        {
            builder.OpenComponent<NxWindowOutlet>(0);
            // Keyed, so a window closing never hands its elements to the next one: nx-window.js holds on to them.
            builder.SetKey(window);
            builder.AddComponentParameter(1, nameof(NxWindowOutlet.Window), window);
            builder.CloseComponent();
        }
    }

    internal void Add(NxWindow window) => windows.Add(window);

    internal void Remove(NxWindow window)
    {
        if (windows.Remove(window))
            StateHasChanged();
    }

    /// <summary>A window rendered: draw its new content.</summary>
    internal void Refresh() => StateHasChanged();

    /// <summary>Stops taking windows; those still open go back in place at their next render.</summary>
    public void Dispose()
    {
        if (state?.Host == this)
            state.Host = null;
    }
}
