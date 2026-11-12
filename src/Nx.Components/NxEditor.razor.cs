using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A rich text (HTML) editor: a toolbar of <see cref="Tools"/> over an editable area. Bold to tables, links and pictures;
/// its own undo history; pasted Word and web content is cleaned. Binds the HTML with <c>@bind-Value</c>, an empty
/// editor being <c>""</c>, and takes part in <c>EditForm</c> validation.
/// </summary>
/// <example>
/// <code>
/// &lt;NxEditor @bind-Value="ticket.Description" Tools="NxEditorTool.All" Height="320px" AriaLabel="Description" /&gt;
/// </code>
/// </example>
public partial class NxEditor : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-editor.js owns the editable area: commands, history, paste cleaning, table edits, and what the caret is in.

    private const string ModulePath = "./_content/Nx.Components/nx-editor.js";
    private const int TablePickerSize = 8;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-editor-{Guid.NewGuid():N}"[..19];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxEditor>? dotNet;
    private ElementReference root;
    private ElementReference content;
    private ElementReference tablePicker;
    private IReadOnlyList<NxEditorTool> toolList = NxEditorTool.Default;
    private EditorState state = new();
    private string? lastValue;
    private string? pendingHtml;
    private bool initialized;
    private bool tablePickerOpen;
    private bool tablePickerShown;
    private bool linkOpen;
    private bool imageOpen;
    private LinkForm link = new();
    private ImageForm image = new();

    /// <summary>Creates the component.</summary>
    public NxEditor() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The HTML. Use <c>@bind-Value</c>. An empty editor is <c>""</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised with the HTML after an edit: <see cref="DebounceDelay"/> after typing stops, on leaving, or at once for a tool.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>Milliseconds after the last keystroke before <see cref="ValueChanged"/> is raised. Default: <c>100</c>.</summary>
    [Parameter] public int DebounceDelay { get; set; } = 100;

    // ── Tools ─────────────────────────────────────────────────────────────────

    /// <summary>The toolbar, in order, with <see cref="NxEditorTool.Separator"/> between groups. Default: <see cref="NxEditorTool.Default"/>; empty hides the toolbar.</summary>
    [Parameter] public IEnumerable<NxEditorTool>? Tools { get; set; }

    /// <summary>The <see cref="NxEditorCustomTool"/>s for the <see cref="NxEditorTool.Custom"/> slots in <see cref="Tools"/>.</summary>
    [Parameter] public RenderFragment? CustomTools { get; set; }

    /// <summary>The text and highlight colors offered. Default: <see cref="NxColorPalettes.Office"/>.</summary>
    [Parameter] public IEnumerable<string>? Colors { get; set; }

    // ── Look and state ────────────────────────────────────────────────────────

    /// <summary>The height of the whole editor, toolbar included; the text scrolls inside. <c>auto</c> grows with the text. Default: <c>250px</c>.</summary>
    [Parameter] public string Height { get; set; } = "250px";

    /// <summary>Text shown while the editor is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Greys the editor out: no editing, no toolbar, out of the tab order.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The text can be read, selected and copied, not changed; the toolbar is off.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>The editable area's accessible name; the toolbar is named after it.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The editable area's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>spellcheck</c>, <c>aria-*</c>…) go to the editable area.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Methods ───────────────────────────────────────────────────────────────

    /// <summary>Moves keyboard focus to the editable area.</summary>
    public ValueTask FocusAsync() => content.FocusAsync();

    /// <summary>Inserts HTML at the caret (at the end when the editor never had one), as one undo step. Raises <see cref="ValueChanged"/>.</summary>
    public async Task InsertHtmlAsync(string html)
    {
        if (module is not null)
            await module.InvokeVoidAsync("insertHtml", ContentId, html);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    internal string ContentId => Id ?? generatedId;
    private bool Interactive => RendererInfo.IsInteractive;
    private bool Editable => !Disabled && !ReadOnly;
    private string ToolBarLabel => AriaLabel is null ? "Formatting" : $"{AriaLabel} formatting";
    private IEnumerable<string> ColorList => Colors ?? NxColorPalettes.Office;
    private int ColorColumns => ColorList.Count() <= 12 ? 6 : 10;
    private string OuterStyle => string.IsNullOrEmpty(Style) ? $"height: {Height}" : $"height: {Height}; {Style}";
    private string? FontSizeValue => FontSizes.FirstOrDefault(f => f.Value == $"{state.FontSize:0.#}pt")?.Value;

    private string? FontFamilyValue => FontFamilies.FirstOrDefault(f =>
        f.Value.Split(',')[0].Trim('\'', '"', ' ').Equals(state.FontFamily, StringComparison.OrdinalIgnoreCase))?.Value;

    private string CssClass
    {
        get
        {
            var css = "nx-editor";
            if (Height == "auto") css += " nx-editor-auto";
            if (Disabled) css += " nx-editor-disabled";
            if (ReadOnly) css += " nx-editor-readonly";
            if (validation.IsInvalid) css += " nx-editor-invalid";
            return string.IsNullOrEmpty(Class) ? css : $"{css} {Class}";
        }
    }

    private string ContentCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-editor-content"
        : $"nx-editor-content {validation.CssClass}";

    private static RenderFragment? IconFor(NxEditorTool tool) =>
        EditorIcons.For(tool.Name) is { } icon ? builder =>
        {
            builder.OpenComponent<NxIcon>(0);
            builder.AddComponentParameter(1, nameof(NxIcon.Icon), icon);
            builder.CloseComponent();
        } : null;

    private bool IsEnabled(NxEditorTool tool) => Editable && Interactive && tool.Name switch
    {
        "undo" => state.CanUndo,
        "redo" => state.CanRedo,
        "unlink" => state.InLink,
        "mergeCells" => state.InTable,
        _ => !tool.IsTableEdit || state.InTable
    };

    private IReadOnlyDictionary<string, object> ButtonAttributes(NxEditorTool tool)
    {
        var attributes = new Dictionary<string, object> { ["data-nx-editor-cmd"] = tool.Name };
        if (Pressed(tool.Name) is { } pressed)
            attributes["aria-pressed"] = pressed ? "true" : "false";
        if (tool.Name == "insertTable")
            attributes["aria-haspopup"] = "dialog";
        return attributes;
    }

    private bool? Pressed(string name) => name switch
    {
        "bold" => state.Bold,
        "italic" => state.Italic,
        "underline" => state.Underline,
        "strikethrough" => state.Strikethrough,
        "unorderedList" => state.UnorderedList,
        "orderedList" => state.OrderedList,
        "alignLeft" => state.Align is "left" or "start",
        "alignCenter" => state.Align == "center",
        "alignRight" => state.Align is "right" or "end",
        "alignJustify" => state.Align == "justify",
        _ => null
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        toolList = Tools?.ToList() ?? NxEditorTool.Default;
        // Only an outside change to Value replaces the text, as in NxTextBox.
        if (!initialized || !string.Equals(Value, lastValue, StringComparison.Ordinal))
        {
            lastValue = Value;
            pendingHtml = Value ?? "";
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!initialized)
        {
            initialized = true;
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            dotNet = DotNetObjectReference.Create(this);
            var html = pendingHtml;
            pendingHtml = null;
            if (module is not null)
                await module.InvokeVoidAsync("init", root, content, dotNet, html ?? "");
            return;
        }
        if (module is null)
            return;
        if (pendingHtml is { } outside)
        {
            pendingHtml = null;
            await module.InvokeVoidAsync("setHtml", ContentId, outside);
        }
        if (tablePickerOpen && !tablePickerShown)
        {
            tablePickerShown = true;
            await module.InvokeVoidAsync("openTablePicker", ContentId, tablePicker, dotNet);
        }
    }

    // ── From the browser ──────────────────────────────────────────────────────

    /// <summary>Called by nx-editor.js with the HTML after an edit.</summary>
    [JSInvokable]
    public Task OnContentChanged(string html) => InvokeAsync(async () =>
    {
        if (string.Equals(html, lastValue ?? "", StringComparison.Ordinal))
            return;
        lastValue = html;
        await ValueChanged.InvokeAsync(html);
        validation.NotifyFieldChanged();
    });

    /// <summary>Called by nx-editor.js when what the caret is in changes (bold, a list, a table…).</summary>
    [JSInvokable]
    public Task OnStateChanged(JsonElement next) => InvokeAsync(() =>
    {
        state = next.Deserialize<EditorState>(JsonSerializerOptions.Web) ?? new EditorState();
        StateHasChanged();
    });

    /// <summary>Called by nx-editor.js for a shortcut handled in .NET (<kbd>Ctrl+K</kbd>).</summary>
    [JSInvokable]
    public Task OnShortcut(string name) => InvokeAsync(async () =>
    {
        var tool = toolList.FirstOrDefault(t => t.Name == name);
        if (tool is null || !IsEnabled(tool))
            return;
        await ExecuteAsync(tool);
        StateHasChanged();
    });

    /// <summary>Called by nx-editor.js when a size is picked in the table grid.</summary>
    [JSInvokable]
    public Task OnTablePicked(int rows, int columns) => InvokeAsync(async () =>
    {
        await CloseTablePickerAsync();
        if (module is not null)
            await module.InvokeVoidAsync("insertTable", ContentId, Math.Clamp(rows, 1, TablePickerSize), Math.Clamp(columns, 1, TablePickerSize));
    });

    /// <summary>Called by nx-editor.js when the table grid closes without a pick (outside press, Escape).</summary>
    [JSInvokable]
    public Task OnTablePickerDismissed() => InvokeAsync(DismissTablePickerAsync);

    private async Task DismissTablePickerAsync()
    {
        await CloseTablePickerAsync();
        if (module is not null)
            await module.InvokeVoidAsync("restoreSelection", ContentId);
    }

    // ── Tools ─────────────────────────────────────────────────────────────────

    private async Task ExecuteAsync(NxEditorTool tool)
    {
        if (module is null || !Editable)
            return;
        switch (tool.Name)
        {
            case "createLink":
                var info = await module.InvokeAsync<LinkInfo?>("linkInfo", ContentId);
                link = new LinkForm { Href = info?.Href, Text = info?.Text, NewWindow = info?.NewWindow == true, Editing = info?.Editing == true };
                linkOpen = true;
                break;
            case "insertImage":
                image = new ImageForm();
                imageOpen = true;
                break;
            case "insertTable":
                if (tablePickerOpen)
                {
                    await DismissTablePickerAsync();
                    return;
                }
                tablePickerOpen = true;
                tablePickerShown = false;
                break;
            default:
                await module.InvokeVoidAsync("exec", ContentId, tool.Name, null);
                break;
        }
    }

    private async Task ApplyAsync(NxEditorTool tool, string? value)
    {
        if (module is not null && Editable)
            await module.InvokeVoidAsync("exec", ContentId, tool.Name, value);
    }

    private async Task CloseTablePickerAsync()
    {
        if (!tablePickerOpen)
            return;
        tablePickerOpen = false;
        if (module is not null && tablePickerShown)
            await module.InvokeVoidAsync("closeTablePicker", ContentId);
        tablePickerShown = false;
        StateHasChanged();
    }

    private async Task LinkKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(link.Href))
            await InsertLinkAsync();
    }

    private async Task InsertLinkAsync()
    {
        linkOpen = false;
        if (module is not null && !string.IsNullOrWhiteSpace(link.Href))
            await module.InvokeVoidAsync("createLink", ContentId, link.Href.Trim(), link.Text ?? "", link.NewWindow);
    }

    private async Task CloseLinkAsync(bool visible)
    {
        if (visible)
            return;
        linkOpen = false;
        if (module is not null)
            await module.InvokeVoidAsync("restoreSelection", ContentId);
    }

    private async Task ImageKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(image.Src))
            await InsertImageAsync();
    }

    private async Task InsertImageAsync()
    {
        imageOpen = false;
        if (module is not null && !string.IsNullOrWhiteSpace(image.Src))
            await module.InvokeVoidAsync("insertImage", ContentId, image.Src.Trim(), image.Alt ?? "", image.Width, image.Height);
    }

    private async Task CloseImageAsync(bool visible)
    {
        if (visible)
            return;
        imageOpen = false;
        if (module is not null)
            await module.InvokeVoidAsync("restoreSelection", ContentId);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", ContentId);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        dotNet?.Dispose();
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    private sealed record Choice(string Text, string Value);

    private static readonly Choice[] Formats =
    [
        new("Paragraph", "p"), new("Heading 1", "h1"), new("Heading 2", "h2"), new("Heading 3", "h3"),
        new("Heading 4", "h4"), new("Heading 5", "h5"), new("Heading 6", "h6"), new("Quotation", "blockquote"),
        new("Preformatted", "pre")
    ];

    private static readonly Choice[] FontSizes =
        [.. new[] { 8, 10, 12, 14, 18, 24, 36 }.Select(s => new Choice($"{s} pt", $"{s}pt"))];

    private static readonly Choice[] FontFamilies =
    [
        new("Arial", "Arial, Helvetica, sans-serif"),
        new("Courier New", "'Courier New', Courier, monospace"),
        new("Georgia", "Georgia, serif"),
        new("Impact", "Impact, Charcoal, sans-serif"),
        new("Lucida Console", "'Lucida Console', Monaco, monospace"),
        new("Tahoma", "Tahoma, Geneva, sans-serif"),
        new("Times New Roman", "'Times New Roman', Times, serif"),
        new("Trebuchet MS", "'Trebuchet MS', Helvetica, sans-serif"),
        new("Verdana", "Verdana, Geneva, sans-serif")
    ];

    // What the caret is in, as nx-editor.js reports it.
    private sealed record EditorState
    {
        /// <summary>Bold at the caret.</summary>
        public bool Bold { get; init; }

        /// <summary>Italic at the caret.</summary>
        public bool Italic { get; init; }

        /// <summary>Underlined at the caret.</summary>
        public bool Underline { get; init; }

        /// <summary>Struck through at the caret.</summary>
        public bool Strikethrough { get; init; }

        /// <summary>In a bulleted list.</summary>
        public bool UnorderedList { get; init; }

        /// <summary>In a numbered list.</summary>
        public bool OrderedList { get; init; }

        /// <summary>The paragraph's alignment.</summary>
        public string? Align { get; init; }

        /// <summary>The block's tag: <c>p</c>, <c>h1</c>…</summary>
        public string? Format { get; init; }

        /// <summary>The font size in points.</summary>
        public double FontSize { get; init; }

        /// <summary>The first font family.</summary>
        public string? FontFamily { get; init; }

        /// <summary>The text color set on the text, as hex.</summary>
        public string? ForeColor { get; init; }

        /// <summary>The highlight set on the text, as hex.</summary>
        public string? BackColor { get; init; }

        /// <summary>In a link.</summary>
        public bool InLink { get; init; }

        /// <summary>In a table cell.</summary>
        public bool InTable { get; init; }

        /// <summary>There is something to undo.</summary>
        public bool CanUndo { get; init; }

        /// <summary>There is something to redo.</summary>
        public bool CanRedo { get; init; }
    }

    private sealed record LinkInfo(string Href, string Text, bool NewWindow, bool Editing);

    private sealed class LinkForm
    {
        public string? Href { get; set; }
        public string? Text { get; set; }
        public bool NewWindow { get; set; }
        public bool Editing { get; set; }
    }

    private sealed class ImageForm
    {
        public string? Src { get; set; }
        public string? Alt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
