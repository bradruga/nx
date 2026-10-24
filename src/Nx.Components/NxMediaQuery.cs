using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Whether a CSS media query matches, as a bindable bool: <c>@bind-Matches</c> follows the browser as the window
/// resizes, the pointer changes or the color scheme flips. Renders nothing. Before the browser answers (prerendering)
/// <see cref="Matches"/> is <c>false</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxMediaQuery Media="(min-width: 992px)" @bind-Matches="isLarge" /&gt;
/// </code>
/// </example>
public sealed class NxMediaQuery : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/Nx.Components/nx-mediaquery.js";

    private IJSObjectReference? module;
    private DotNetObjectReference<NxMediaQuery>? selfReference;
    private int? watchId;
    private string? watchedMedia;
    private bool matches;
    private bool known;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The media query, e.g. <c>(min-width: 768px)</c>, <c>(pointer: coarse)</c>, <c>(prefers-color-scheme: dark)</c>.</summary>
    [Parameter, EditorRequired] public string Media { get; set; } = "";

    /// <summary>Whether the query matches. Use <c>@bind-Matches</c>; the component sets it, never the other way.</summary>
    [Parameter] public bool Matches { get; set; }

    /// <summary>Raised with the match state once the browser answers, and on every change after that.</summary>
    [Parameter] public EventCallback<bool> MatchesChanged { get; set; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            selfReference = DotNetObjectReference.Create(this);
        }
        if (module is null || selfReference is null || watchedMedia == Media)
            return;

        // A new query: stop the old one and report the new state, even when it is the same as before.
        if (watchId is int old)
            await module.InvokeVoidAsync("unwatch", old);
        watchedMedia = Media;
        var state = await module.InvokeAsync<WatchResult>("watch", Media, selfReference);
        watchId = state.Id;
        await ReportAsync(state.Matches, force: true);
    }

    /// <summary>Called by nx-mediaquery.js when the match state changes. Not for application use.</summary>
    [JSInvokable]
    public Task OnMediaChanged(bool value) => InvokeAsync(() => ReportAsync(value, force: false));

    private async Task ReportAsync(bool value, bool force)
    {
        if (known && value == matches && !force)
            return;
        known = true;
        matches = value;
        await MatchesChanged.InvokeAsync(value);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                if (watchId is int id)
                    await module.InvokeVoidAsync("unwatch", id);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }

    internal sealed record WatchResult(int Id, bool Matches);
}
