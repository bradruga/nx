using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A start and end date pair with one calendar: two months side by side on a desktop, a full-screen sheet on a
/// phone. The first pick sets the start, the second the end. Same typing, format, limits and validation as
/// <see cref="NxDatePicker{TValue}"/>. Bind with <c>@bind-StartValue</c> and <c>@bind-EndValue</c>.
/// </summary>
/// <typeparam name="TValue">The bound date type: <see cref="DateTime"/>, <see cref="DateTimeOffset"/> or <see cref="DateOnly"/>, nullable or not.</typeparam>
/// <example>
/// <code>
/// &lt;NxDateRangePicker Label="Pour window" @bind-StartValue="job.PourFrom" @bind-EndValue="job.PourTo" /&gt;
/// </code>
/// </example>
public partial class NxDateRangePicker<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // As NxDatePicker, through nx-picker.js (the edit text of each field is swapped in by the browser on focus).
    // The popup spans both fields and closes on a pointer press outside them, since focus moving between the two
    // inputs must not close it.

    private const string ModulePath = "./_content/Nx.Components/nx-picker.js";
    private const int PopupMaxHeight = 480;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Side
    {
        public ElementReference Input;
        public ElementReference Toggle;
        public string Text = "";
        public DateOnly? Current;       // as last seen or raised
        public bool HasFocus;
    }

    private readonly FieldValidation startValidation;
    private readonly FieldValidation endValidation;
    private readonly BrowserTimeZone zone = new();
    private readonly string generatedId = $"nx-daterange-{Guid.NewGuid():N}"[..22];
    private readonly Side start = new();
    private readonly Side end = new();
    private IJSObjectReference? module;
    private DotNetObjectReference<NxDateRangePicker<TValue>>? selfReference;
    private ElementReference fields;
    private ElementReference popup;
    private ElementReference popupBody;
    private NxCalendar? calendar;
    private DateFormatter formatter = null!;
    private bool activeEnd;             // the next pick sets the end
    private bool editOnFocusApplied;
    private bool isNarrow;
    private bool popupOpen, popupPlaced, closePending;
    private bool sheetOpen, sheetFocusPending, scrollLockPending;
    private Side? focusPending;

    /// <summary>Creates the component.</summary>
    public NxDateRangePicker()
    {
        startValidation = new FieldValidation(StateHasChanged);
        endValidation = new FieldValidation(StateHasChanged);
    }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Values ────────────────────────────────────────────────────────────────

    /// <summary>The first day of the range. Use <c>@bind-StartValue</c>.</summary>
    [Parameter] public TValue? StartValue { get; set; }

    /// <summary>Raised when the start is committed.</summary>
    [Parameter] public EventCallback<TValue?> StartValueChanged { get; set; }

    /// <summary>Identifies the start field for <c>EditForm</c> validation. Set by <c>@bind-StartValue</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? StartValueExpression { get; set; }

    /// <summary>The last day of the range. Use <c>@bind-EndValue</c>.</summary>
    [Parameter] public TValue? EndValue { get; set; }

    /// <summary>Raised when the end is committed.</summary>
    [Parameter] public EventCallback<TValue?> EndValueChanged { get; set; }

    /// <summary>Identifies the end field for <c>EditForm</c> validation. Set by <c>@bind-EndValue</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? EndValueExpression { get; set; }

    /// <summary>Earliest allowed date.</summary>
    [Parameter] public DateOnly? Min { get; set; }

    /// <summary>Latest allowed date.</summary>
    [Parameter] public DateOnly? Max { get; set; }

    /// <summary>Returns <c>true</c> for dates that cannot be picked as a start or end.</summary>
    [Parameter] public Func<DateOnly, bool>? IsDateDisabled { get; set; }

    // ── Format and calendar ───────────────────────────────────────────────────

    /// <summary>How the dates show while not focused; see <see cref="NxDatePicker{TValue}.Format"/>. Default: <c>"d"</c>.</summary>
    [Parameter] public string Format { get; set; } = "d";

    /// <summary>Culture for the format, typing order, names and first day of week. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Months side by side in the popup: 1 or 2. The phone sheet always shows one. Default: <c>2</c>.</summary>
    [Parameter] public int Months { get; set; } = 2;

    /// <summary>Shows week numbers in the calendar.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>The calendar's first column. Default: the culture's first day of the week.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    // ── Labels ────────────────────────────────────────────────────────────────

    /// <summary>Label of the whole range, above both fields.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Label of the start field. Default: "Start".</summary>
    [Parameter] public string StartLabel { get; set; } = "Start";

    /// <summary>Label of the end field. Default: "End".</summary>
    [Parameter] public string EndLabel { get; set; } = "End";

    /// <summary>Placeholder of both fields. Default: the edit pattern, e.g. <c>mm/dd/yyyy</c>.</summary>
    [Parameter] public string? Placeholder { get; set; }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>Shows a clear button in each field while it has a date. Nullable types only.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Disables both fields.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Makes both fields read-only; the calendar does not open.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>When the calendar opens as a full-screen sheet. Default: <see cref="NxMobileMode.Auto"/>.</summary>
    [Parameter] public NxMobileMode MobileMode { get; set; } = NxMobileMode.Auto;

    /// <summary>With <see cref="NxMobileMode.Auto"/>, the viewport width in pixels at or below which the sheet is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The sheet's title. Default: <see cref="Label"/>, else "Choose dates".</summary>
    [Parameter] public string? MobileTitle { get; set; }

    // ── Attributes ────────────────────────────────────────────────────────────

    /// <summary>Base <c>id</c>: the inputs get <c>{Id}-start</c> and <c>{Id}-end</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Moves keyboard focus to the start input.</summary>
    public ValueTask FocusAsync() => start.Input.FocusAsync();

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private string BaseId => Id ?? generatedId;
    private string InputIdOf(bool isEnd) => $"{BaseId}-{(isEnd ? "end" : "start")}";
    private string LabelId => $"{BaseId}-label";
    private string CalendarId => $"{BaseId}-calendar";
    private bool Editable => !Disabled && !ReadOnly;
    private bool ShowClear => Clearable && DateConversion<TValue>.IsNullable && Editable;
    private string SheetTitle => MobileTitle ?? Label ?? "Choose dates";

    private bool UseSheet => MobileMode switch
    {
        NxMobileMode.Always => true,
        NxMobileMode.Never  => false,
        _                   => isNarrow
    };

    private string InputCssClass(bool isEnd)
    {
        var css = (isEnd ? endValidation : startValidation).CssClass;
        return string.IsNullOrEmpty(css) ? "nx-daterange-input" : $"nx-daterange-input {css}";
    }

    private string RootClass => string.Join(' ', new[]
    {
        "nx-daterangepicker",
        popupOpen || sheetOpen ? "nx-daterange-open" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private bool Editing(Side side) => side.HasFocus && Editable;
    private string TextOf(Side side) => Editing(side) ? formatter.Edit(side.Current) : formatter.Display(side.Current);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!DateConversion<TValue>.IsSupported)
            throw new InvalidOperationException(DateConversion<TValue>.Unsupported("NxDateRangePicker"));

        startValidation.Attach(CascadedEditContext, StartValueExpression);
        endValidation.Attach(CascadedEditContext, EndValueExpression);
        formatter = new DateFormatter(Format, EffectiveCulture);
        Sync(start, DateConversion<TValue>.ToDate(StartValue));
        Sync(end, DateConversion<TValue>.ToDate(EndValue));
    }

    private void Sync(Side side, DateOnly? incoming)
    {
        if (incoming != side.Current || (!side.HasFocus && side.Text != formatter.Display(side.Current)))
        {
            side.Current = incoming;
            side.Text = TextOf(side);
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
            selfReference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("init", start.Input);
            await module.InvokeVoidAsync("init", end.Input);
            await module.InvokeVoidAsync("editOnFocus", start.Input, false);
            await module.InvokeVoidAsync("editOnFocus", end.Input, false);
            editOnFocusApplied = true;
            if (DateConversion<TValue>.IsOffset)
                await zone.LoadAsync(module);
            if (MobileMode == NxMobileMode.Auto)
            {
                isNarrow = await module.InvokeAsync<bool>("watchViewport", fields, selfReference, MobileBreakpoint);
                if (isNarrow)
                    StateHasChanged();
            }
        }
        if (module is null)
            return;

        if (closePending)
        {
            closePending = false;
            await module.InvokeVoidAsync("close", fields);
        }
        if (popupOpen && !popupPlaced)
        {
            popupPlaced = true;
            await module.InvokeAsync<string>("open", fields, popup, popupBody, PopupMaxHeight, selfReference, nameof(OnOutsidePointerDown));
            StateHasChanged();
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
        if (focusPending is { } side)
        {
            focusPending = null;
            await (sheetOpen || UseSheet ? side.Toggle : side.Input).FocusAsync();
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

    /// <summary>Called by nx-popup.js on a pointer press outside the fields and popup. Not for application use.</summary>
    [JSInvokable]
    public void OnOutsidePointerDown() => InvokeAsync(() =>
    {
        ClosePopup();
        StateHasChanged();
    });

    // ── Opening and closing ───────────────────────────────────────────────────

    private void Open(Side side)
    {
        if (!Editable || popupOpen || sheetOpen)
            return;
        activeEnd = side == end && start.Current is not null;
        if (UseSheet)
        {
            sheetOpen = true;
            sheetFocusPending = true;
            scrollLockPending = true;
            return;
        }
        popupOpen = true;
        popupPlaced = false;
        if (!side.HasFocus)
            focusPending = side;
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
        focusPending = activeEnd ? end : start;
    }

    // ── Committing ────────────────────────────────────────────────────────────

    /// <summary>As NxDatePicker: empty is null, unreadable or disabled reverts, outside Min/Max moves to the limit. A start after the end swaps them.</summary>
    private async Task CommitTextAsync(Side side)
    {
        if (!formatter.TryParse(side.Text, out var parsed) || (parsed is null && !DateConversion<TValue>.IsNullable))
        {
            side.Text = TextOf(side);
            return;
        }
        if (parsed is DateOnly d)
        {
            if (Min is DateOnly min && d < min) d = min;
            if (Max is DateOnly max && d > max) d = max;
            if (IsDateDisabled?.Invoke(d) == true)
            {
                side.Text = TextOf(side);
                return;
            }
            parsed = d;
        }
        var other = side == start ? end : start;
        if (parsed is DateOnly p && other.Current is DateOnly o && (side == start ? p > o : p < o))
        {
            await CommitAsync(other, p);
            await CommitAsync(side, o);
            return;
        }
        await CommitAsync(side, parsed);
    }

    private async Task CommitAsync(Side side, DateOnly? date)
    {
        var changed = date != side.Current;
        side.Current = date;
        side.Text = TextOf(side);
        if (!changed)
            return;
        if (side == start)
        {
            if (DateConversion<TValue>.IsOffset)
                await zone.WhenLoadedAsync();
            await StartValueChanged.InvokeAsync(DateConversion<TValue>.FromDate(date, StartValue, zone));
            startValidation.NotifyFieldChanged();
        }
        else
        {
            if (DateConversion<TValue>.IsOffset)
                await zone.WhenLoadedAsync();
            await EndValueChanged.InvokeAsync(DateConversion<TValue>.FromDate(date, EndValue, zone));
            endValidation.NotifyFieldChanged();
        }
    }

    /// <summary>The first pick sets the start (dropping an end before it), the second the end; a second pick before the start restarts.</summary>
    private async Task HandleCalendarPick(DateOnly? picked)
    {
        if (picked is not DateOnly date)
            return;
        if (!activeEnd || (start.Current is DateOnly s && date < s))
        {
            await CommitAsync(start, date);
            if (end.Current is DateOnly e && e < date)
                await CommitAsync(end, null);
            activeEnd = true;
            if (popupOpen)
                focusPending = end;
            return;
        }
        await CommitAsync(end, date);
        activeEnd = false;
        ClosePopup();
        CloseSheet();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleInputAfter(Side side)
    {
        side.Text ??= "";
        if (popupOpen && calendar is not null && formatter.TryParse(side.Text, out var typed) && typed is DateOnly d)
            calendar.GoTo(d);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e, Side side)
    {
        if (!Editable)
            return;
        if (popupOpen)
        {
            switch (e.Key)
            {
                // Tab between the two fields keeps the calendar; out of the pair closes it.
                case "Tab" when side == start ? e.ShiftKey : !e.ShiftKey:
                case "Escape":
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
            case "ArrowDown":
                Open(side);
                break;
            case "Enter":
                await CommitTextAsync(side);
                break;
            case "Escape" when ShowClear && side.Current is not null:
                await CommitAsync(side, null);
                break;
        }
    }

    private void HandleToggleClick(Side side)
    {
        if (popupOpen)
            ClosePopup();
        else
            Open(side);
    }

    private void HandleFocus(Side side)
    {
        side.HasFocus = true;
        if (popupOpen)
            activeEnd = side == end && start.Current is not null;
        // nx-input.js has already swapped in the edit text; only before it has loaded is that done here.
        if (!editOnFocusApplied && Editing(side) && side.Text == formatter.Display(side.Current))
            side.Text = formatter.Edit(side.Current);
    }

    private async Task HandleBlur(Side side)
    {
        side.HasFocus = false;
        await CommitTextAsync(side);
        side.Text = formatter.Display(side.Current);
    }

    private async Task HandleClearClick(Side side)
    {
        await CommitAsync(side, null);
        await side.Input.FocusAsync();
    }

    private async Task HandleSheetClear()
    {
        await CommitAsync(start, null);
        await CommitAsync(end, null);
        activeEnd = false;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        startValidation.Dispose();
        endValidation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("close", fields);
                await module.InvokeVoidAsync("dispose", start.Input);
                await module.InvokeVoidAsync("dispose", end.Input);
                await module.InvokeVoidAsync("unwatchViewport", fields);
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
