using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A date input for <see cref="DateTime"/>, <see cref="DateTimeOffset"/> or <see cref="DateOnly"/>, nullable or not: a date typed freely in the
/// culture's order and read when committed (<c>12/5</c> is this year's), a calendar popup (month, year and decade
/// views) with min/max, disabled dates, week numbers and Today, and the same label and clear options as
/// <see cref="NxTextBox"/>. Up and Down step the day, month or year under the caret; Alt+Down opens the calendar.
/// On a phone the calendar opens as a full-screen sheet. <see cref="ShowCalendar"/> off leaves the field alone.
/// </summary>
/// <typeparam name="TValue">The bound date type. A <see cref="DateTime"/> keeps its time of day; a <see cref="DateTimeOffset"/> its offset too, and a new one takes the browser's.</typeparam>
/// <example>
/// <code>
/// &lt;NxDatePicker Label="Pour date" @bind-Value="order.PourDate" Min="DateOnly.FromDateTime(DateTime.Today)" /&gt;
/// </code>
/// </example>
public partial class NxDatePicker<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-picker.js places the popup, cancels the caret moves of the keys the open calendar uses, locks page
    // scroll under the phone sheet and, while the calendar is closed, steps the date part under the caret. On focus it
    // swaps the display text for the edit text (data-nx-edit) in the browser, so the swap cannot land on typed text.
    // Parsing and the calendar itself are C#.

    private const string ModulePath = "./_content/Nx.Components/nx-picker.js";
    private const int PopupMaxHeight = 480;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly BrowserTimeZone zone = new();
    private readonly string generatedId = $"nx-datepicker-{Guid.NewGuid():N}"[..23];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxDatePicker<TValue>>? selfReference;
    private ElementReference input;
    private ElementReference toggle;
    private ElementReference popup;
    private ElementReference popupBody;
    private NxCalendar? calendar;
    private string currentText = "";
    private DateOnly? current;          // the value as last seen or raised
    private DateFormatter formatter = null!;
    private string? appliedStepShape;
    private bool hasFocus;
    private bool editOnFocusApplied;
    private bool isNarrow;
    private bool popupOpen, popupPlaced, closePending;
    private bool sheetOpen, sheetFocusPending, scrollLockPending, returnFocusPending;

    /// <summary>Creates the component.</summary>
    public NxDatePicker() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The date. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the value is committed: on picking a date, on Enter, on leaving the field.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Earliest allowed date. Typed dates before it are moved up to it.</summary>
    [Parameter] public DateOnly? Min { get; set; }

    /// <summary>Latest allowed date. Typed dates after it are moved back to it.</summary>
    [Parameter] public DateOnly? Max { get; set; }

    /// <summary>Returns <c>true</c> for dates that cannot be picked, e.g. weekends. Typing one reverts the text.</summary>
    [Parameter] public Func<DateOnly, bool>? IsDateDisabled { get; set; }

    // ── Format ────────────────────────────────────────────────────────────────

    /// <summary>
    /// How the date shows while the field is not focused: a .NET date format such as <c>"d"</c>, <c>"yyyy-MM-dd"</c>
    /// or <c>"ddd, MMM d, yyyy"</c>. While focused, an all-number form of it is edited; a format with
    /// names edits in the culture's short date. Default: <c>"d"</c>.
    /// </summary>
    [Parameter] public string Format { get; set; } = "d";

    /// <summary>Culture for the format, typing order, month and day names, first day of week. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    // ── Calendar ──────────────────────────────────────────────────────────────

    /// <summary>Shows the calendar button and opens the calendar. <c>false</c>: a typed date only. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowCalendar { get; set; } = true;

    /// <summary>Shows week numbers in the calendar.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>The calendar's first column. Default: the culture's first day of the week.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>Shows the calendar's Today button. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowTodayButton { get; set; } = true;

    // ── Label and text ────────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Placeholder text shown while the field is empty. Default: the edit pattern, e.g. <c>mm/dd/yyyy</c>.</summary>
    [Parameter] public string? Placeholder { get; set; }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>Shows a clear button while there is a date. Nullable types only.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute; the calendar does not open.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the input after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

    /// <summary>When the calendar opens as a full-screen sheet instead of a popup. Default: <see cref="NxMobileMode.Auto"/>.</summary>
    [Parameter] public NxMobileMode MobileMode { get; set; } = NxMobileMode.Auto;

    /// <summary>With <see cref="NxMobileMode.Auto"/>, the viewport width in pixels at or below which the sheet is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The sheet's title. Default: <see cref="Label"/>, else "Choose a date".</summary>
    [Parameter] public string? MobileTitle { get; set; }

    // ── Attributes and events ─────────────────────────────────────────────────

    /// <summary>The input's <c>id</c>. Generated when not set, so the label always points at it.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-*</c>…) are passed to the <c>&lt;input&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised when the input gains focus.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the input loses focus, after the typed date is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Text ──────────────────────────────────────────────────────────────────

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private string FormatDisplay(DateOnly? date) => formatter.Display(date);
    private string FormatEdit(DateOnly? date) => formatter.Edit(date);
    private bool TryParse(string text, out DateOnly? date) => formatter.TryParse(text, out date);

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private string CalendarId => $"{InputId}-calendar";
    private bool Editable => !Disabled && !ReadOnly;
    private bool Editing => hasFocus && Editable;
    private bool ShowClear => Clearable && DateConversion<TValue>.IsNullable && Editable && current is not null;
    private string SheetTitle => MobileTitle ?? Label ?? "Choose a date";

    private bool UseSheet => MobileMode switch
    {
        NxMobileMode.Always => true,
        NxMobileMode.Never  => false,
        _                   => isNarrow
    };

    private string? PlaceholderText => Placeholder ?? formatter.Hint;

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-datepicker-input"
        : $"nx-datepicker-input {validation.CssClass}";

    private string? RootClass => popupOpen || sheetOpen ? $"nx-datepicker-open {Class}".Trim() : Class;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!DateConversion<TValue>.IsSupported)
            throw new InvalidOperationException(DateConversion<TValue>.Unsupported("NxDatePicker"));

        validation.Attach(CascadedEditContext, ValueExpression);
        formatter = new DateFormatter(Format, EffectiveCulture);

        var incoming = DateConversion<TValue>.ToDate(Value);
        if (incoming != current || (!hasFocus && currentText != FormatDisplay(current)))
        {
            current = incoming;
            currentText = Editing ? FormatEdit(current) : FormatDisplay(current);
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
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is null)
                return;
            await module.InvokeVoidAsync("init", input);
            await module.InvokeVoidAsync("editOnFocus", input, false);
            editOnFocusApplied = true;
            if (DateConversion<TValue>.IsOffset)
                await zone.LoadAsync(module);
            if (MobileMode == NxMobileMode.Auto)
            {
                selfReference = DotNetObjectReference.Create(this);
                isNarrow = await module.InvokeAsync<bool>("watchViewport", input, selfReference, MobileBreakpoint);
                if (isNarrow)
                    StateHasChanged();
            }
            if (AutoFocus)
                await input.FocusAsync();
        }
        if (module is null)
            return;

        if (appliedStepShape != formatter.Shape)
        {
            appliedStepShape = formatter.Shape;
            await module.InvokeVoidAsync("stepDate", input, appliedStepShape, formatter.Order);
        }
        if (closePending)
        {
            closePending = false;
            await module.InvokeVoidAsync("close", input);
        }
        if (popupOpen && !popupPlaced)
        {
            popupPlaced = true;
            await module.InvokeAsync<string>("open", input, popup, popupBody, PopupMaxHeight, null, null);
            StateHasChanged();      // the calendar now exists: point aria-activedescendant at its cell
        }
        if (scrollLockPending)
        {
            scrollLockPending = false;
            await module.InvokeVoidAsync("lockScroll", sheetOpen);
        }
        if (sheetFocusPending && sheetOpen && calendar is not null)
        {
            sheetFocusPending = false;
            await calendar.FocusAsync();
        }
        if (returnFocusPending)
        {
            returnFocusPending = false;
            await toggle.FocusAsync();
        }
    }

    /// <summary>Called by nx-popup.js when the viewport crosses <see cref="MobileBreakpoint"/>. Not for application use.</summary>
    [JSInvokable]
    public void OnViewportChanged(bool narrow)
    {
        if (narrow == isNarrow)
            return;
        isNarrow = narrow;
        InvokeAsync(StateHasChanged);
    }

    // ── Opening and closing ───────────────────────────────────────────────────

    private async Task OpenAsync()
    {
        if (!ShowCalendar || !Editable || popupOpen || sheetOpen)
            return;
        if (UseSheet)
        {
            sheetOpen = true;
            sheetFocusPending = true;
            scrollLockPending = true;
            return;
        }
        popupOpen = true;
        popupPlaced = false;
        if (!hasFocus)
            await input.FocusAsync();
    }

    private void ClosePopup()
    {
        if (!popupOpen)
            return;
        popupOpen = false;
        closePending = true;
    }

    private void CloseSheet()
    {
        if (!sheetOpen)
            return;
        sheetOpen = false;
        scrollLockPending = true;
        returnFocusPending = true;
    }

    // ── Committing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the typed text. Empty is null (kept as is for non-nullable types); unreadable text and disabled
    /// dates revert; dates outside Min/Max are moved to the nearest limit.
    /// </summary>
    private async Task CommitTextAsync()
    {
        if (!TryParse(currentText, out var parsed) || (parsed is null && !DateConversion<TValue>.IsNullable))
        {
            currentText = Editing ? FormatEdit(current) : FormatDisplay(current);
            return;
        }
        if (parsed is DateOnly d)
        {
            if (Min is DateOnly min && d < min) d = min;
            if (Max is DateOnly max && d > max) d = max;
            if (IsDateDisabled?.Invoke(d) == true)
            {
                currentText = Editing ? FormatEdit(current) : FormatDisplay(current);
                return;
            }
            parsed = d;
        }
        await CommitAsync(parsed);
    }

    private async Task CommitAsync(DateOnly? date)
    {
        var changed = date != current;
        current = date;
        currentText = Editing ? FormatEdit(date) : FormatDisplay(date);
        if (!changed)
            return;
        if (DateConversion<TValue>.IsOffset)
            await zone.WhenLoadedAsync();
        await ValueChanged.InvokeAsync(DateConversion<TValue>.FromDate(date, Value, zone));
        validation.NotifyFieldChanged();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // @bind protects typed text from a slower render. Nothing commits until change; an open calendar
    // follows the typed date.
    private void HandleInputAfter()
    {
        currentText ??= "";
        if (popupOpen && calendar is not null && TryParse(currentText, out var typed) && typed is DateOnly d)
            calendar.GoTo(d);
    }

    private Task HandleChange() => CommitTextAsync();

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!Editable)
            return;
        if (popupOpen)
        {
            switch (e.Key)
            {
                case "Escape" or "Tab":
                case "ArrowUp" when e.AltKey:
                    ClosePopup();
                    return;
                case "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown" or "PageUp" or "PageDown" or "Enter":
                    if (calendar is not null)
                        await calendar.HandleKeyAsync(e);
                    return;
            }
            return;
        }
        switch (e.Key)
        {
            // Up and Down step the date part (nx-picker.js); a format with nothing to step still opens on Down.
            case "ArrowDown" when e.AltKey || formatter.Shape is null:
                await OpenAsync();
                break;
            case "Enter":
                await CommitTextAsync();
                break;
            case "Escape" when ShowClear:
                await CommitAsync(null);
                break;
        }
    }

    private async Task HandleToggleClick()
    {
        if (popupOpen)
            ClosePopup();
        else
            await OpenAsync();
    }

    private async Task HandleCalendarPick(DateOnly? date)
    {
        await CommitAsync(date);
        ClosePopup();
        CloseSheet();
    }

    private async Task HandleFocus(FocusEventArgs e)
    {
        hasFocus = true;
        // nx-input.js has already swapped in the edit text; only before it has loaded is that done here.
        if (!editOnFocusApplied && Editing && currentText == FormatDisplay(current))
            currentText = FormatEdit(current);
        await OnFocus.InvokeAsync(e);
    }

    private async Task HandleBlur(FocusEventArgs e)
    {
        hasFocus = false;
        ClosePopup();
        await CommitTextAsync();
        currentText = FormatDisplay(current);
        await OnBlur.InvokeAsync(e);
    }

    private async Task HandleClearClick()
    {
        await CommitAsync(null);
        await input.FocusAsync();
    }

    private async Task HandleSheetClear()
    {
        await CommitAsync(null);
        CloseSheet();
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
                if (sheetOpen)
                    await module.InvokeVoidAsync("lockScroll", false);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
