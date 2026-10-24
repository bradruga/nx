using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Nx.Components;

/// <summary>
/// Shows one item at a time, sliding to the next every <see cref="AutomaticPageChangeInterval"/> milliseconds:
/// arrows, page dots, a pause button, swipe and the arrow keys. Rotation pauses while the pointer is over it or
/// focus is in it, and after the last item it starts again. Size it with <see cref="Width"/> and <see cref="Height"/>.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <example>
/// <code>
/// &lt;NxCarousel Data="pictures" Height="360px" AutomaticPageChangeInterval="6000"&gt;
///     &lt;Template&gt;&lt;img src="@context.Url" alt="@context.Caption" /&gt;&lt;/Template&gt;
/// &lt;/NxCarousel&gt;
/// </code>
/// </example>
public partial class NxCarousel<TItem> : IDisposable
{
    private const double SwipeDistance = 40;

    private List<TItem> items = [];
    private int pageIndex;
    private int? lastPage, lastInterval;
    private bool interactive, hovered, focused, paused, jump, ticking;
    private double? pointerStartX;
    private Timer? timer;

    /// <summary>The items, one slide each.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Renders a slide. Default: the item's text.</summary>
    [Parameter] public RenderFragment<TItem>? Template { get; set; }

    /// <summary>The slide shown, from <c>1</c>. Use <c>@bind-Page</c>.</summary>
    [Parameter] public int Page { get; set; } = 1;

    /// <summary>Raised when another slide is shown, by the user or the rotation.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>Moves to the next slide on its own. Default: <c>true</c>.</summary>
    [Parameter] public bool AutomaticPageChange { get; set; } = true;

    /// <summary>Milliseconds each slide shows before the next. Default: <c>5000</c>.</summary>
    [Parameter] public int AutomaticPageChangeInterval { get; set; } = 5000;

    /// <summary>Shows the previous and next arrows. Default: <c>true</c>.</summary>
    [Parameter] public bool Arrows { get; set; } = true;

    /// <summary>Shows a dot per slide that goes to it. Default: <c>true</c>.</summary>
    [Parameter] public bool Pageable { get; set; } = true;

    /// <summary>Shows a button that stops and starts the rotation. Default: on when it rotates and has dots.</summary>
    [Parameter] public bool? ShowPauseButton { get; set; }

    /// <summary>CSS width, e.g. <c>100%</c> or <c>640px</c>. Default: <c>100%</c>.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>CSS height, e.g. <c>360px</c> or <c>100%</c>. Default: <c>320px</c>.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>The carousel's accessible name. Default: "Carousel".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The carousel's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Whether the rotation is moving now: on, not paused, and the pointer and focus elsewhere.</summary>
    public bool Running => interactive && AutomaticPageChange && !paused && !hovered && !focused && items.Count > 1;

    private bool PauseShown => AutomaticPageChange && (ShowPauseButton ?? Pageable);

    private string RootStyle => $"--nx-carousel-width: {Width ?? "100%"}; --nx-carousel-height: {Height ?? "320px"}; --nx-carousel-index: {pageIndex}; {Style}".TrimEnd(' ', ';');
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-carousel" : $"nx-carousel {Class}";
    private string TrackClass => jump ? "nx-carousel-track nx-carousel-track-jump" : "nx-carousel-track";

    /// <summary>Shows the next slide (the first after the last).</summary>
    public Task NextAsync() => GoAsync(pageIndex + 1);

    /// <summary>Shows the previous slide (the last before the first).</summary>
    public Task PreviousAsync() => GoAsync(pageIndex - 1);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        items = Data?.ToList() ?? [];
        // Only an outside change of Page moves the carousel.
        if (lastPage != Page)
        {
            lastPage = Page;
            pageIndex = Page - 1;
        }
        pageIndex = items.Count == 0 ? 0 : Math.Clamp(pageIndex, 0, items.Count - 1);
        // A new interval counts from now, rather than waiting out the old one.
        var intervalChanged = lastInterval is not null && lastInterval != AutomaticPageChangeInterval;
        lastInterval = AutomaticPageChangeInterval;
        Schedule(restart: intervalChanged);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender)
            return;
        interactive = true;     // prerendered pages do not rotate
        Schedule();
    }

    /// <summary>Starts the countdown to the next slide (afresh with <paramref name="restart"/>), or stops it when the rotation is not running.</summary>
    private void Schedule(bool restart = false)
    {
        if (!Running)
        {
            ticking = false;
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }
        if (ticking && !restart)
            return;
        ticking = true;
        var due = Math.Max(500, AutomaticPageChangeInterval);
        timer ??= new Timer(_ => InvokeAsync(() => GoAsync(pageIndex + 1)));
        timer.Change(due, Timeout.Infinite);
    }

    private async Task GoAsync(int index)
    {
        if (items.Count == 0)
            return;
        var next = ((index % items.Count) + items.Count) % items.Count;
        // Wrapping round jumps instead of sliding back across every slide.
        jump = next != index;
        if (next != pageIndex)
        {
            pageIndex = next;
            StateHasChanged();
            await PageChanged.InvokeAsync(pageIndex + 1);
        }
        Schedule(restart: true);
    }

    private void SetHover(bool value)
    {
        hovered = value;
        Schedule();
    }

    private void SetFocus(bool value)
    {
        focused = value;
        Schedule();
    }

    private void TogglePause()
    {
        paused = !paused;
        Schedule();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "ArrowLeft")
            await GoAsync(pageIndex - 1);
        else if (e.Key == "ArrowRight")
            await GoAsync(pageIndex + 1);
    }

    private void HandlePointerDown(PointerEventArgs e) => pointerStartX = e.ClientX;

    private async Task HandlePointerUp(PointerEventArgs e)
    {
        if (pointerStartX is not double start)
            return;
        pointerStartX = null;
        var moved = e.ClientX - start;
        if (Math.Abs(moved) >= SwipeDistance)
            await GoAsync(moved < 0 ? pageIndex + 1 : pageIndex - 1);
    }

    /// <inheritdoc />
    public void Dispose() => timer?.Dispose();
}
