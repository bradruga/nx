using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Text typed into a fixed pattern — a bid number <c>00-0000</c>, a phone <c>(000) 000-0000</c>. Each <see cref="Mask"/>
/// rule character is a slot that takes only its kind of character; the literals between are filled in for you. Typing
/// is masked in the browser; the value is committed on Enter and on leaving the field, as in <see cref="NxTextBox"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxMaskedTextBox Label="Bid number" Mask="00-0000" @bind-Value="bid.Number" IncludeLiterals="true" MaskOnFocus="true" /&gt;
/// </code>
/// </example>
public partial class NxMaskedTextBox : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js's editMask makes every edit in the browser (the next slot that takes a character, Backspace
    // and Delete emptying slots) and reports the whole masked text through @bind. Values are formatted and read
    // back here, with MaskPattern.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-masked-{Guid.NewGuid():N}"[..19];
    private IJSObjectReference? module;
    private ElementReference input;
    private MaskPattern pattern = new("");
    private string? patternMask;       // the mask MaskPattern was built for
    private char patternPrompt;
    private string? appliedMask;        // the mask nx-input.js was given
    private char appliedPrompt;
    private string currentText = "";
    private string? current;            // the value as last seen or raised
    private bool hasFocus;

    /// <summary>Creates the component.</summary>
    public NxMaskedTextBox() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The value. Use <c>@bind-Value</c>. Without <see cref="IncludeLiterals"/> it is the typed characters only.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when the value is committed: on Enter, on leaving the field, on clear.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    // ── Mask ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The pattern. Slots: <c>0</c> digit, <c>9</c> digit or space, <c>#</c> digit, space, + or -, <c>L</c> letter,
    /// <c>?</c> letter or space, <c>A</c> letter or digit, <c>a</c> letter, digit or space, <c>&amp;</c> any character
    /// but a space, <c>C</c> any character. Anything else is a literal; <c>\</c> makes the next character one.
    /// </summary>
    [Parameter, EditorRequired] public string Mask { get; set; } = "";

    /// <summary>The character shown in an empty slot. Default: <c>_</c>.</summary>
    [Parameter] public char Prompt { get; set; } = '_';

    /// <summary>The value keeps the literals: <c>26-0412</c> rather than <c>260412</c>.</summary>
    [Parameter] public bool IncludeLiterals { get; set; }

    /// <summary>An empty field shows the mask only while focused, else its <see cref="Placeholder"/>.</summary>
    [Parameter] public bool MaskOnFocus { get; set; }

    // ── Label and behavior ────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Text shown while the field is empty (with <see cref="MaskOnFocus"/>, while not focused).</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear button while there is a value.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>The input's <c>id</c>. Generated when not set, so the label always points at it.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-*</c>…) are passed to the <c>&lt;input&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised when the input gains focus.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the input loses focus, after the value is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Every slot is filled.</summary>
    public bool IsComplete => pattern.IsComplete(pattern.Format(current, IncludeLiterals, Prompt), Prompt);

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private bool Editable => !Disabled && !ReadOnly;
    private bool ShowClear => Clearable && Editable && current is not null;

    /// <summary>A mask of digits only brings up the number pad.</summary>
    private string? InputMode => Mask.Length > 0 && Mask.Where(MaskPattern.IsRule).All(c => c is '0' or '9') ? "numeric" : null;

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-maskedtextbox-input"
        : $"nx-maskedtextbox-input {validation.CssClass}";

    /// <summary>The text for a value: the masked text, or nothing for an empty value that shows its mask only on focus.</summary>
    private string TextFor(string? value) =>
        value is null && MaskOnFocus && !hasFocus ? "" : pattern.Format(value, IncludeLiterals, Prompt);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        var maskChanged = patternMask != Mask || patternPrompt != Prompt;
        if (maskChanged)
        {
            pattern = new MaskPattern(Mask);
            patternMask = Mask;
            patternPrompt = Prompt;
        }

        var incoming = pattern.Extract(pattern.Format(Value, IncludeLiterals, Prompt), IncludeLiterals, Prompt);
        if (maskChanged || incoming != current || (!hasFocus && currentText != TextFor(current)))
        {
            current = incoming;
            currentText = TextFor(current);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is not null && (appliedMask != Mask || appliedPrompt != Prompt))
        {
            appliedMask = Mask;
            appliedPrompt = Prompt;
            await module.InvokeVoidAsync("editMask", input, Mask, Prompt.ToString());
        }
    }

    // ── Committing ────────────────────────────────────────────────────────────

    private async Task CommitAsync(string? value)
    {
        var changed = value != current;
        current = value;
        if (!changed)
            return;
        await ValueChanged.InvokeAsync(value);
        validation.NotifyFieldChanged();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // @bind protects typed text from a slower render. Nothing commits until change.
    private void HandleInputAfter() => currentText ??= "";

    private Task HandleChange() => CommitAsync(pattern.Extract(currentText, IncludeLiterals, Prompt));

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!Editable)
            return;
        if (e.Key == "Enter")
            await CommitAsync(pattern.Extract(currentText, IncludeLiterals, Prompt));
        else if (e.Key == "Escape" && ShowClear)
            await HandleClearClick();
    }

    private async Task HandleFocus(FocusEventArgs e)
    {
        hasFocus = true;
        await OnFocus.InvokeAsync(e);
    }

    private async Task HandleBlur(FocusEventArgs e)
    {
        hasFocus = false;
        await CommitAsync(pattern.Extract(currentText, IncludeLiterals, Prompt));
        currentText = TextFor(current);
        await OnBlur.InvokeAsync(e);
    }

    private async Task HandleClearClick()
    {
        currentText = hasFocus || !MaskOnFocus ? pattern.Template(Prompt) : "";
        await CommitAsync(null);
        await input.FocusAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", input);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
    }
}
