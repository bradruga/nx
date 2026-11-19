using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A single-line text input with a label, prefix and suffix slots, a clear button, a password
/// toggle, a character counter and <c>EditForm</c> validation. Bind with <c>@bind-Value</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxTextBox Label="Customer" @bind-Value="order.Customer" Clearable="true" /&gt;
/// &lt;ValidationMessage For="() =&gt; order.Customer" /&gt;
/// </code>
/// </example>
public partial class NxTextBox : IAsyncDisposable
{
    // nx-input.js marks the Escape that clears as used, so a window around the field stays open. Loaded only when Clearable.
    private const string ModulePath = "./_content/Nx.Components/nx-input.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? module;
    private bool escapeApplied;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-textbox-{Guid.NewGuid():N}"[..20];
    private ElementReference input;
    private CancellationTokenSource? debounce;
    private string currentText = "";
    private string? lastValue;
    private bool hasRendered;
    private bool showPassword;

    /// <summary>Creates the component.</summary>
    public NxTextBox() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The text. Use <c>@bind-Value</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when the value is committed — on change, or on input when <see cref="Immediate"/>.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// When <c>true</c>, the value updates as the user types. When <c>false</c> (default) it updates
    /// on change: when the field loses focus or the user presses Enter.
    /// </summary>
    [Parameter] public bool Immediate { get; set; }

    /// <summary>
    /// With <see cref="Immediate"/>, waits this many milliseconds after the last keystroke before
    /// updating the value. Leaving the field updates it at once. Default: <c>0</c> (no delay).
    /// </summary>
    [Parameter] public int DebounceDelay { get; set; }

    // ── Label and text ────────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Placeholder text shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Content inside the field before the text: an icon, a currency sign, a unit.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary>Content inside the field after the text and before the built-in buttons.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    // ── Input ─────────────────────────────────────────────────────────────────

    /// <summary>The HTML input type. Default: <see cref="NxTextBoxType.Text"/>.</summary>
    [Parameter] public NxTextBoxType Type { get; set; } = NxTextBoxType.Text;

    /// <summary>The on-screen keyboard hint (<c>inputmode</c>). Default: <c>null</c>, no attribute.</summary>
    [Parameter] public NxInputMode? InputMode { get; set; }

    /// <summary>The <c>autocomplete</c> attribute, e.g. <c>"off"</c>, <c>"email"</c>, <c>"current-password"</c>.</summary>
    [Parameter] public string? AutoComplete { get; set; }

    /// <summary>Maximum number of characters. The browser stops input at the limit.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>Shows a <c>12 / 50</c> counter under the field. Requires <see cref="MaxLength"/>.</summary>
    [Parameter] public bool ShowCounter { get; set; }

    // ── Buttons ───────────────────────────────────────────────────────────────

    /// <summary>Shows a clear button while there is text. <kbd>Escape</kbd> also clears; in a window, that press does not close it.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>With <see cref="NxTextBoxType.Password"/>, shows a button that reveals the text. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowPasswordToggle { get; set; } = true;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute: focusable and selectable, not editable.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the input after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

    // ── Attributes and events ─────────────────────────────────────────────────

    /// <summary>The input's <c>id</c>. Generated when not set, so the label always points at it.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-*</c>, <c>pattern</c>…) are passed to the <c>&lt;input&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised when the input gains focus.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the input loses focus, after any pending value is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private string CounterId => $"{InputId}-counter";
    private bool HasText => currentText.Length > 0;
    private bool Editable => !Disabled && !ReadOnly;
    private bool ShowClear => Clearable && Editable && HasText;
    private bool ShowToggle => Type == NxTextBoxType.Password && ShowPasswordToggle && !Disabled;
    private bool ShowCounterText => ShowCounter && MaxLength is not null;

    private string InputType => Type switch
    {
        NxTextBoxType.Password => showPassword ? "text" : "password",
        NxTextBoxType.Email    => "email",
        NxTextBoxType.Search   => "search",
        NxTextBoxType.Tel      => "tel",
        NxTextBoxType.Url      => "url",
        _                      => "text"
    };

    private string? InputModeAttribute => InputMode?.ToString().ToLowerInvariant();

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-textbox-input"
        : $"nx-textbox-input {validation.CssClass}";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);

        // Only an outside change to Value replaces the text; re-renders with the same Value must not
        // overwrite what the user is typing (on-change mode, or while a debounce is pending).
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
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        hasRendered = true;
        if (firstRender && AutoFocus)
            await input.FocusAsync();
        if (Clearable && !escapeApplied)
        {
            escapeApplied = true;
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("escapeClears", input);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // The input uses @bind rather than value + @oninput: only @bind tells the renderer that the
    // event updates the value, which stops a slower render from writing stale text over keystrokes.
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

    private void TogglePassword() => showPassword = !showPassword;

    private async Task ClearAsync(bool focus)
    {
        CancelDebounce();
        currentText = "";
        await CommitAsync("");
        if (focus)
            await input.FocusAsync();
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
            await module.InvokeVoidAsync("dispose", input);
            await module.DisposeAsync();
        }
        catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
