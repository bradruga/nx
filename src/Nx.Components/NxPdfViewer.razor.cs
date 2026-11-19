using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Shows a PDF from bytes (<see cref="Data"/>) or an address (<see cref="Url"/>), drawn by pdf.js: selectable text,
/// links, find with highlighted matches, zoom, page by page or scrolling, download and print, under an Nx toolbar.
/// pdf.js (Apache-2.0) ships with the library in <c>_content/Nx.Components/pdfjs</c>; nothing loads from elsewhere.
/// </summary>
/// <example>
/// <code>
/// &lt;NxPdfViewer Data="report.Bytes" FileName="Pour report.pdf" Height="100%" /&gt;
/// </code>
/// </example>
public partial class NxPdfViewer : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-pdfviewer.js loads pdf.js on first use, opens the document, and reports pages, the zoom and find matches.

    private const string ModulePath = "./_content/Nx.Components/nx-pdfviewer.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-pdf-{Guid.NewGuid():N}"[..16];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxPdfViewer>? dotNet;
    private ElementReference root;
    private ElementReference container;
    private object? shownSource;
    private bool sourceChanged = true;
    private Status status = Status.Empty;
    private string? error;
    private int pages;
    private int page = 1;
    private double scale = 1;
    private string? preset;
    private string? query;
    private int matchCurrent;
    private int matchTotal;

    private enum Status { Empty, Loading, Loaded, Failed }

    // ── Document ──────────────────────────────────────────────────────────────

    /// <summary>The PDF's bytes. Takes precedence over <see cref="Url"/>; a new array opens it again.</summary>
    [Parameter] public byte[]? Data { get; set; }

    /// <summary>The PDF's address (same origin, or a server that allows it).</summary>
    [Parameter] public string? Url { get; set; }

    /// <summary>The name a download is saved under. Default: <c>document.pdf</c>.</summary>
    [Parameter] public string? FileName { get; set; }

    /// <summary>How the pages are sized when a document opens. Default: <see cref="NxPdfViewerZoom.Auto"/>.</summary>
    [Parameter] public NxPdfViewerZoom Zoom { get; set; }

    /// <summary>The page in view. Use <c>@bind-Page</c> to follow it or to jump to a page.</summary>
    [Parameter] public int Page { get; set; } = 1;

    /// <summary>Raised as the page in view changes.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    // ── Look ──────────────────────────────────────────────────────────────────

    /// <summary>The toolbar's tool groups. Default: <see cref="NxPdfViewerTools.All"/>; <see cref="NxPdfViewerTools.None"/> hides it.</summary>
    [Parameter] public NxPdfViewerTools Tools { get; set; } = NxPdfViewerTools.All;

    /// <summary>The viewer's height, toolbar included; <c>100%</c> fills a container with a height. Default: <c>600px</c>.</summary>
    [Parameter] public string Height { get; set; } = "600px";

    /// <summary>The viewer's width. Default: the container's.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Shown while there is no document. Default: "No document".</summary>
    [Parameter] public string? NoDataText { get; set; } = "No document";

    /// <summary>The document's accessible name. Default: <see cref="FileName"/>.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes go to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised with the page count once a document is open.</summary>
    [Parameter] public EventCallback<int> OnLoad { get; set; }

    /// <summary>Raised with pdf.js's message when a document cannot be opened.</summary>
    [Parameter] public EventCallback<string> OnError { get; set; }

    // ── Methods ───────────────────────────────────────────────────────────────

    /// <summary>Saves the document under <see cref="FileName"/>.</summary>
    public async Task DownloadAsync()
    {
        if (module is not null && Loaded)
            await module.InvokeVoidAsync("download", BaseId, FileName ?? "document.pdf");
    }

    /// <summary>Opens the browser's print dialog for the document.</summary>
    public async Task PrintAsync()
    {
        if (module is not null && Loaded)
            await module.InvokeVoidAsync("print", BaseId);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string BaseId => Id ?? generatedId;
    private bool Loaded => status == Status.Loaded;
    private bool Has(NxPdfViewerTools tool) => (Tools & tool) == tool;
    private string ErrorText => string.IsNullOrEmpty(error) ? "This document could not be opened." : $"This document could not be opened: {error}";
    private string? MatchText => string.IsNullOrEmpty(query) || !Loaded ? null : matchTotal == 0 ? "No matches" : $"{matchCurrent} of {matchTotal}";

    private string OuterStyle
    {
        get
        {
            var css = $"height: {Height}";
            if (!string.IsNullOrEmpty(Width)) css += $"; width: {Width}";
            return string.IsNullOrEmpty(Style) ? css : $"{css}; {Style}";
        }
    }

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-pdfviewer" : $"nx-pdfviewer {Class}";

    private static RenderFragment Icon(NxIconData icon) => builder =>
    {
        builder.OpenComponent<NxIcon>(0);
        builder.AddComponentParameter(1, nameof(NxIcon.Icon), icon);
        builder.CloseComponent();
    };

    private sealed record ZoomChoice(string Text, string Value);

    private static readonly ZoomChoice[] Presets =
    [
        new("Automatic", "auto"), new("Page width", "page-width"), new("Page fit", "page-fit"), new("Actual size", "page-actual"),
        .. new[] { 50, 75, 100, 125, 150, 200, 300, 400 }.Select(p => new ZoomChoice($"{p}%", (p / 100.0).ToString(CultureInfo.InvariantCulture)))
    ];

    // A preset, a listed percentage, or the current one added to the list (after zooming in steps).
    private string ZoomValue => preset ?? Math.Round(scale, 2).ToString(CultureInfo.InvariantCulture);

    private IEnumerable<ZoomChoice> ZoomChoices =>
        Presets.Any(z => z.Value == ZoomValue) ? Presets : [.. Presets, new($"{Math.Round(scale * 100)}%", ZoomValue)];

    private static string ZoomName(NxPdfViewerZoom zoom) => zoom switch
    {
        NxPdfViewerZoom.PageWidth => "page-width",
        NxPdfViewerZoom.PageFit => "page-fit",
        NxPdfViewerZoom.ActualSize => "page-actual",
        _ => "auto"
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        object? source = Data is { Length: > 0 } ? Data : string.IsNullOrEmpty(Url) ? null : Url;
        if (!ReferenceEquals(source, shownSource) && !Equals(source, shownSource))
        {
            shownSource = source;
            sourceChanged = true;
        }
        // An outside page change scrolls to it; the viewer's own report coming back is not one.
        if (Loaded && module is not null && Page != page && Page >= 1 && Page <= pages)
        {
            page = Page;
            await module.InvokeVoidAsync("setPage", BaseId, Page);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            dotNet = DotNetObjectReference.Create(this);
            if (module is not null)
                await module.InvokeVoidAsync("init", BaseId, container, dotNet);
        }
        if (module is null || !sourceChanged)
            return;
        sourceChanged = false;
        await OpenAsync();
    }

    private async Task OpenAsync()
    {
        query = null;
        matchCurrent = matchTotal = 0;
        pages = 0;
        page = 1;
        switch (shownSource)
        {
            case byte[] bytes:
                status = Status.Loading;
                StateHasChanged();
                using (var stream = new DotNetStreamReference(new MemoryStream(bytes)))
                    await module!.InvokeVoidAsync("openData", BaseId, stream, ZoomName(Zoom));
                break;
            case string url:
                status = Status.Loading;
                StateHasChanged();
                await module!.InvokeVoidAsync("openUrl", BaseId, url, ZoomName(Zoom));
                break;
            default:
                status = Status.Empty;
                await module!.InvokeVoidAsync("close", BaseId);
                StateHasChanged();
                break;
        }
    }

    // ── From the browser ──────────────────────────────────────────────────────

    /// <summary>Called by nx-pdfviewer.js once the document is open.</summary>
    [JSInvokable]
    public Task OnLoaded(int pageCount) => InvokeAsync(async () =>
    {
        status = Status.Loaded;
        pages = pageCount;
        error = null;
        StateHasChanged();
        await OnLoad.InvokeAsync(pageCount);
        if (Page > 1 && Page <= pageCount && module is not null)
            await module.InvokeVoidAsync("setPage", BaseId, Page);
    });

    /// <summary>Called by nx-pdfviewer.js when the document cannot be opened.</summary>
    [JSInvokable]
    public Task OnFailed(string message) => InvokeAsync(async () =>
    {
        status = Status.Failed;
        error = message;
        StateHasChanged();
        await OnError.InvokeAsync(message);
    });

    /// <summary>Called by nx-pdfviewer.js as the page in view or the zoom changes.</summary>
    [JSInvokable]
    public Task OnViewChanged(int pageNumber, double currentScale, string? currentPreset) => InvokeAsync(async () =>
    {
        scale = currentScale;
        preset = string.IsNullOrEmpty(currentPreset) ? null : currentPreset;
        var pageMoved = pageNumber != page;
        page = pageNumber;
        StateHasChanged();
        if (pageMoved)
            await PageChanged.InvokeAsync(pageNumber);
    });

    /// <summary>Called by nx-pdfviewer.js with the find matches.</summary>
    [JSInvokable]
    public Task OnMatches(int current, int total) => InvokeAsync(() =>
    {
        matchCurrent = current;
        matchTotal = total;
        StateHasChanged();
    });

    // ── Tools ─────────────────────────────────────────────────────────────────

    private async Task GoToPageAsync(int target)
    {
        if (module is null || !Loaded)
            return;
        target = Math.Clamp(target, 1, pages);
        await module.InvokeVoidAsync("setPage", BaseId, target);
    }

    private async Task ZoomStepAsync(bool zoomIn)
    {
        if (module is not null && Loaded)
            await module.InvokeVoidAsync("zoomStep", BaseId, zoomIn);
    }

    private async Task SetZoomAsync(string? value)
    {
        if (module is not null && Loaded && !string.IsNullOrEmpty(value))
            await module.InvokeVoidAsync("setZoom", BaseId, value);
    }

    private async Task SearchAsync(string? text)
    {
        query = text;
        if (string.IsNullOrEmpty(text))
            matchCurrent = matchTotal = 0;
        if (module is not null && Loaded)
            await module.InvokeVoidAsync("find", BaseId, text ?? "", false, false);
    }

    private async Task FindAgainAsync(bool previous)
    {
        if (module is not null && Loaded && !string.IsNullOrEmpty(query))
            await module.InvokeVoidAsync("find", BaseId, query, true, previous);
    }

    private Task SearchKeyDownAsync(KeyboardEventArgs e) =>
        e.Key == "Enter" ? FindAgainAsync(e.ShiftKey) : Task.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", BaseId);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        dotNet?.Dispose();
    }
}
