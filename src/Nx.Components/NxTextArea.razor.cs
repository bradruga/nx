using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Multi-line text with the same field as <see cref="NxTextBox"/> — label, clear button, character
/// counter, <c>EditForm</c> validation, binding on change or as you type with a debounce — plus <see cref="Rows"/>,
/// <see cref="AutoResize"/> between <see cref="MinRows"/> and <see cref="MaxRows"/>, and a resize handle.
/// </summary>
/// <example>
/// <code>
/// &lt;NxTextArea Label="Notes" @bind-Value="pour.Notes" AutoResize="true" MaxRows="8" MaxLength="500" ShowCounter="true" /&gt;
/// </code>
/// </example>
public partial class NxTextArea : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js grows the textarea with its text (AutoResize). Everything else is the NxTextBox model.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-textarea-{Guid.NewGuid():N}"[..21];
    private IJSObjectReference? module;
    private ElementReference textarea;
    private CancellationTokenSource? debounce;
    private string currentText = "";
    private string? lastValue;
    private bool hasRendered;

    /// <summary>Creates the component.</summary>
    public NxTextArea() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The text. Use <c>@bind-Value</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when the value is committed — on leaving the field, or as you type with <see cref="Immediate"/>.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>Updates the value as the user types instead of on leaving the field.</summary>
    [Parameter] public bool Immediate { get; set; }

    /// <summary>With <see cref="Immediate"/>, milliseconds to wait after the last keystroke. Default: <c>0</c>.</summary>
    [Parameter] public int DebounceDelay { get; set; }

    // ── Size ──────────────────────────────────────────────────────────────────

    /// <summary>Visible lines. With <see cref="AutoResize"/>, the starting height when <see cref="MinRows"/> is not set. Default: <c>3</c>.</summary>
    [Parameter] public int Rows { get; set; } = 3;

    /// <summary>Grows and shrinks with the text, between <see cref="MinRows"/> and <see cref="MaxRows"/> lines, then scrolls.</summary>
    [Parameter] public bool AutoResize { get; set; }

    /// <summary>With <see cref="AutoResize"/>, the fewest lines. Default: <see cref="Rows"/>.</summary>
    [Parameter] public int? MinRows { get; set; }

    /// <summary>With <see cref="AutoResize"/>, the most lines before it scrolls. Default: no limit.</summary>
    [Parameter] public int? MaxRows { get; set; }

    /// <summary>The drag handle in the corner. Default: <see cref="NxTextAreaResize.Vertical"/>; none with <see cref="AutoResize"/>.</summary>
    [Parameter] public NxTextAreaResize Resize { get; set; } = NxTextAreaResize.Vertical;

    // ── Label and text ────────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the textarea.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Placeholder text shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Maximum number of characters. The browser stops input at the limit.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>Shows a <c>120 / 500</c> counter under the field. Requires <see cref="MaxLength"/>.</summary>
    [Parameter] public bool ShowCounter { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Shows a clear button while there is text. <kbd>Escape</kbd> also clears.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the textarea after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

    // ── Attributes and events ─────────────────────────────────────────────────

    /// <summary>The textarea's <c>id</c>. Generated when not set, so the label always points at it.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-*</c>, <c>spellcheck</c>…) go to the <c>&lt;textarea&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised when the textarea gains focus.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the textarea loses focus, after any pending value is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the textarea.</summary>
    public ValueTask FocusAsync() => textarea.FocusAsync();

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private string CounterId => $"{InputId}-counter";
    private bool Editable => !Disabled && !ReadOnly;
    private bool ShowClear => Clearable && Editable && currentText.Length > 0;
    private bool ShowCounterText => ShowCounter && MaxLength is not null;
    private string ResizeName => AutoResize ? "none" : Resize.ToString().ToLowerInvariant();

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-textarea-input"
        : $"nx-textarea-input {validation.CssClass}";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        // Only an outside change to Value replaces the text, as in NxTextBox.
        if (!hasRendered || !string.Equals(Value, lastValue, StringComparison.Ordinal))
        {
            CancelDebounce();
            lastValue = Value;
            currentText = Value ?? "";
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        hasRendered = true;
        if (firstRender && AutoFocus)
            await textarea.FocusAsync();
        if (!AutoResize)
            return;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        // Every render: a value set from code changes the text without an input event.
        if (module is not null)
            await module.InvokeVoidAsync("autosize", textarea, MinRows ?? Rows, MaxRows);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // @bind, not value + @oninput, as in NxTextBox: protects typed text from a slower render.
    private async Task HandleInputAfter()
    {
        currentText ??= "";
        if (!Immediate)
            return;
        if (DebounceDelay <= 0)
        {
            await CommitAsync(currentText);
            return;
        }
        CancelDebounce();
        var cts = debounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(DebounceDelay, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        await CommitAsync(currentText);
    }

    private async Task HandleChange(ChangeEventArgs e)
    {
        currentText = e.Value?.ToString() ?? "";
        CancelDebounce();
        await CommitAsync(currentText);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && ShowClear)
            await ClearAsync(focus: false);
    }

    private Task HandleClearClick() => ClearAsync(focus: true);

    private async Task ClearAsync(bool focus)
    {
        CancelDebounce();
        currentText = "";
        await CommitAsync("");
        if (focus)
            await textarea.FocusAsync();
    }

    private async Task CommitAsync(string text)
    {
        if (string.Equals(text, lastValue ?? "", StringComparison.Ordinal))
            return;
        lastValue = text;
        await ValueChanged.InvokeAsync(text);
        validation.NotifyFieldChanged();
    }

    private void CancelDebounce()
    {
        debounce?.Cancel();
        debounce?.Dispose();
        debounce = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        CancelDebounce();
        validation.Dispose();
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", textarea);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
