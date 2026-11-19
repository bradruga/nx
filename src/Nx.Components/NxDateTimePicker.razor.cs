using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A date and time input for <see cref="DateTime"/> or <see cref="DateTimeOffset"/>, nullable or not: typed in any .NET
/// format, or picked from a popup with <see cref="NxDatePicker{TValue}"/>'s calendar beside a list of times every
/// <see cref="TimeStep"/> minutes. Min and max are date-times. On a phone the popup is a full-screen sheet with Date
/// and Time tabs; the time is picked from hour and minute columns (<see cref="NxTimeColumns"/>).
/// </summary>
/// <typeparam name="TValue"><see cref="DateTime"/> or <see cref="DateTimeOffset"/>, nullable or not. A <see cref="DateTimeOffset"/> keeps its offset; a new one takes the browser's.</typeparam>
/// <example>
/// <code>
/// &lt;NxDateTimePicker Label="Gate opens" @bind-Value="schedule.OpenTime" Format="M/d/yyyy h:mm tt" TimeStep="15" /&gt;
/// </code>
/// </example>
public partial class NxDateTimePicker<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // As NxDatePicker, through nx-picker.js: the mask of an all-number format, popup placement, the keys the
    // open popup takes over, scrolling the time list, the sheet's scroll lock, and swapping in the edit text on focus.

    private const string ModulePath = "./_content/Nx.Components/nx-picker.js";
    private const int PopupMaxHeight = 480;
    private const int TimePageSize = 6;

    /// <summary>Where the open popup's keys go.</summary>
    private enum DateTimePane { Date, Time }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly BrowserTimeZone zone = new();
    private readonly string generatedId = $"nx-datetimepicker-{Guid.NewGuid():N}"[..27];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxDateTimePicker<TValue>>? selfReference;
    private ElementReference input;
    private ElementReference toggle;
    private ElementReference popup;
    private ElementReference popupBody;
    private ElementReference timeList;
    private NxCalendar? calendar;
    private DateTimeFormatter formatter = null!;
    private string? appliedMask;
    private string currentText = "";
    private DateTime? current;              // the value as last seen or raised
    private List<TimeOnly> times = [];
    private int activeTime = -1;
    private TimeOnly sheetTime;             // what the sheet's time columns show; Done commits it
    private DateTimePane pane;
    private bool hasFocus;
    private bool editOnFocusApplied;
    private bool isNarrow;
    private bool popupOpen, popupPlaced, closePending, scrollPending;
    private bool sheetOpen, sheetFocusPending, scrollLockPending, returnFocusPending;

    /// <summary>Creates the component.</summary>
    public NxDateTimePicker() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The date and time. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the value is committed: on picking a day or a time, on Enter, on leaving the field.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Earliest allowed date and time. Earlier values are moved up to it.</summary>
    [Parameter] public DateTime? Min { get; set; }

    /// <summary>Latest allowed date and time. Later values are moved back to it.</summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>Returns <c>true</c> for days that cannot be picked. Typing one reverts the text.</summary>
    [Parameter] public Func<DateOnly, bool>? IsDateDisabled { get; set; }

    /// <summary>Minutes between the times in the list. Typed times need not be on a step; the phone sheet's minute column lists every 5 minutes, or every <c>TimeStep</c> under 5. Default: <c>30</c>.</summary>
    [Parameter] public int TimeStep { get; set; } = 30;

    // ── Format ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A .NET date and time format, e.g. <c>"g"</c>, <c>"M/d/yyyy h:mm tt"</c> or <c>"yyyy-MM-dd HH:mm:ss"</c>. An
    /// all-number 24-hour format is typed behind a mask; others are typed freely and read leniently. Default: <c>"g"</c>.
    /// </summary>
    [Parameter] public string Format { get; set; } = "g";

    /// <summary>Culture for the format, typing, month and day names, AM/PM. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    // ── Calendar ──────────────────────────────────────────────────────────────

    /// <summary>Shows week numbers in the calendar.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>The calendar's first column. Default: the culture's first day of the week.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>Shows the calendar's Today button. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowTodayButton { get; set; } = true;

    // ── Label and text ────────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Placeholder text shown while the field is empty. Default: the pattern, e.g. <c>m/d/yyyy h:mm am/pm</c>.</summary>
    [Parameter] public string? Placeholder { get; set; }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>Shows a clear button while there is a value. Nullable types only.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute; the popup does not open.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the input after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

    /// <summary>When the popup opens as a full-screen sheet. Default: <see cref="NxMobileMode.Auto"/>.</summary>
    [Parameter] public NxMobileMode MobileMode { get; set; } = NxMobileMode.Auto;

    /// <summary>With <see cref="NxMobileMode.Auto"/>, the viewport width in pixels at or below which the sheet is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The sheet's title. Default: <see cref="Label"/>, else "Choose a date and time".</summary>
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

    /// <summary>Raised when the input loses focus, after the typed value is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private string InputId => Id ?? generatedId;
    private string PopupId => $"{InputId}-popup";
    private string CalendarId => $"{InputId}-calendar";
    private string TimeListId => $"{InputId}-times";
    private string TimeOptionId(int index) => $"{InputId}-time-{index}";
    private bool Editable => !Disabled && !ReadOnly;
    private bool Editing => hasFocus && Editable;
    private bool ShowClear => Clearable && DateTimeConversion<TValue>.IsNullable && Editable && current is not null;
    private string SheetTitle => MobileTitle ?? Label ?? "Choose a date and time";
    private string? PlaceholderText => Placeholder ?? formatter.Hint;
    private DateOnly? MinDate => Min is DateTime m ? DateOnly.FromDateTime(m) : null;
    private DateOnly? MaxDate => Max is DateTime m ? DateOnly.FromDateTime(m) : null;
    private DateOnly? CurrentDate => current is DateTime d ? DateOnly.FromDateTime(d) : null;
    private TimeOnly? CurrentTime => current is DateTime d ? new TimeOnly(d.Hour, d.Minute) : null;

    /// <summary>The day the time list is for: the value's, else today within Min and Max.</summary>
    private DateOnly ListDate => CurrentDate ?? ClampDate(DateOnly.FromDateTime(DateTime.Today));

    private bool UseSheet => MobileMode switch
    {
        NxMobileMode.Always => true,
        NxMobileMode.Never  => false,
        _                   => isNarrow
    };

    private string? ActiveDescendant => !popupOpen ? null
        : pane == DateTimePane.Time ? (activeTime >= 0 ? TimeOptionId(activeTime) : null)
        : calendar?.ActiveDescendant;

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-datetimepicker-input"
        : $"nx-datetimepicker-input {validation.CssClass}";

    private string? RootClass => popupOpen || sheetOpen ? $"nx-datetimepicker-open {Class}".Trim() : Class;

    private string PaneClass(DateTimePane which) =>
        pane == which ? "nx-datetimepicker-pane nx-datetimepicker-pane-active" : "nx-datetimepicker-pane";

    private string SheetTabClass(DateTimePane which) =>
        pane == which ? "nx-datetimepicker-sheet-tab nx-datetimepicker-sheet-tab-active" : "nx-datetimepicker-sheet-tab";

    private string TimeTitle => CurrentDate?.ToString("ddd, MMM d", EffectiveCulture) ?? "Time";
    private string SheetDateText => CurrentDate?.ToString("d", EffectiveCulture) ?? "—";
    private string SheetTimeText => pane == DateTimePane.Time ? FormatTime(sheetTime) : CurrentTime is TimeOnly t ? FormatTime(t) : "—";

    // The sheet's time columns are for one day: Min and Max limit them only on their own days.
    private TimeOnly? MinTime => Min is DateTime m && DateOnly.FromDateTime(m) == ListDate ? new TimeOnly(m.Hour, m.Minute) : null;
    private TimeOnly? MaxTime => Max is DateTime m && DateOnly.FromDateTime(m) == ListDate ? new TimeOnly(m.Hour, m.Minute) : null;

    private string FormatTime(TimeOnly time) => time.ToString("t", EffectiveCulture);
    private bool Is24Hour => !EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains('h');

    private string TimeClass(int index, TimeOnly time) => string.Join(' ', new[]
    {
        "nx-timepicker-item",
        index >= 0 && index == activeTime && pane == DateTimePane.Time ? "nx-timepicker-item-active" : null,
        time == CurrentTime ? "nx-timepicker-item-selected" : null
    }.Where(c => c is not null));

    private DateOnly ClampDate(DateOnly date)
    {
        if (MinDate is DateOnly min && date < min) return min;
        if (MaxDate is DateOnly max && date > max) return max;
        return date;
    }

    private DateTime Clamp(DateTime value)
    {
        if (Min is DateTime min && value < min) return min;
        if (Max is DateTime max && value > max) return max;
        return value;
    }

    /// <summary>Every <see cref="TimeStep"/> minutes of <paramref name="date"/>, within Min and Max on their own days.</summary>
    private List<TimeOnly> BuildTimes(DateOnly date)
    {
        var step = Math.Clamp(TimeStep, 1, 720);
        // Min counts from the start of its minute, so a Min of 9:00:30 still lists 9:00.
        var min = Min is DateTime m0 ? m0.AddTicks(-(m0.Ticks % TimeSpan.TicksPerMinute)) : (DateTime?)null;
        var result = new List<TimeOnly>();
        for (var m = 0; m < 24 * 60; m += step)
        {
            var time = new TimeOnly(m / 60, m % 60);
            var at = date.ToDateTime(time);
            if (at >= min.GetValueOrDefault(DateTime.MinValue) && at <= Max.GetValueOrDefault(DateTime.MaxValue))
                result.Add(time);
        }
        return result;
    }

    /// <summary>The time at or just after <paramref name="time"/> (else now), so the list opens where the user is.</summary>
    private int NearestTime(TimeOnly? time)
    {
        if (times.Count == 0)
            return -1;
        var target = time ?? TimeOnly.FromDateTime(DateTime.Now);
        var index = times.FindIndex(t => t >= target);
        return index < 0 ? times.Count - 1 : index;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!DateTimeConversion<TValue>.IsSupported)
            throw new InvalidOperationException($"NxDateTimePicker supports DateTime and DateTimeOffset, nullable or not; not {typeof(TValue).Name}.");

        validation.Attach(CascadedEditContext, ValueExpression);
        formatter = new DateTimeFormatter(Format, EffectiveCulture);

        var incoming = DateTimeConversion<TValue>.ToDateTime(Value);
        if (incoming != current || (!hasFocus && currentText != formatter.Display(current)))
        {
            current = incoming;
            currentText = Editing ? formatter.Edit(current) : formatter.Display(current);
        }
        times = BuildTimes(ListDate);
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
            if (DateTimeConversion<TValue>.IsOffset)
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

        if (appliedMask != formatter.Mask)
        {
            appliedMask = formatter.Mask;
            await module.InvokeVoidAsync("mask", input, appliedMask);
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
            scrollPending = true;
            StateHasChanged();      // the calendar now exists: point aria-activedescendant at its cell
        }
        if (popupOpen && scrollPending)
        {
            scrollPending = false;
            await module.InvokeVoidAsync("scrollActive", timeList, false);
        }
        if (scrollLockPending)
        {
            scrollLockPending = false;
            await module.InvokeVoidAsync("lockScroll", sheetOpen);
        }
        if (sheetFocusPending && sheetOpen)
        {
            sheetFocusPending = false;
            // The time columns take focus themselves as they appear.
            if (pane == DateTimePane.Date && calendar is not null)
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
        if (!Editable || popupOpen || sheetOpen)
            return;
        pane = DateTimePane.Date;
        times = BuildTimes(ListDate);
        activeTime = NearestTime(CurrentTime);
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

    private void ShowSheetPane(DateTimePane which)
    {
        pane = which;
        sheetFocusPending = true;
        ResetSheetTime();
    }

    /// <summary>The columns open on the value's time, else on the list's time nearest to now, within the day's limits.</summary>
    private void ResetSheetTime()
    {
        sheetTime = CurrentTime ?? (times.Count > 0 ? times[NearestTime(null)] : TimeOnly.MinValue);
        if (MinTime is TimeOnly min && sheetTime < min) sheetTime = min;
        if (MaxTime is TimeOnly max && sheetTime > max) sheetTime = max;
    }

    // ── Committing ────────────────────────────────────────────────────────────

    /// <summary>Empty is null (kept as is for non-nullable types); unreadable text and disabled days revert; outside Min/Max moves to the limit.</summary>
    private async Task CommitTextAsync()
    {
        if (!formatter.TryParse(currentText, out var parsed) || (parsed is null && !DateTimeConversion<TValue>.IsNullable) ||
            (parsed is DateTime p && IsDateDisabled?.Invoke(DateOnly.FromDateTime(Clamp(p))) == true))
        {
            currentText = Editing ? formatter.Edit(current) : formatter.Display(current);
            return;
        }
        await CommitAsync(parsed is DateTime d ? Clamp(d) : null);
    }

    private async Task CommitAsync(DateTime? value)
    {
        var changed = value != current;
        current = value;
        currentText = Editing ? formatter.Edit(value) : formatter.Display(value);
        times = BuildTimes(ListDate);
        if (!changed)
            return;
        if (DateTimeConversion<TValue>.IsOffset)
            await zone.WhenLoadedAsync();
        await ValueChanged.InvokeAsync(DateTimeConversion<TValue>.FromDateTime(value, Value, zone));
        validation.NotifyFieldChanged();
    }

    /// <summary>A day from the calendar keeps the time of day (midnight when there is none); the time list is next.</summary>
    private async Task HandleCalendarPick(DateOnly? date)
    {
        if (date is not DateOnly d)
            return;
        var time = current is DateTime c ? TimeOnly.FromDateTime(c) : TimeOnly.MinValue;
        await CommitAsync(Clamp(d.ToDateTime(time)));
        pane = DateTimePane.Time;
        activeTime = NearestTime(CurrentTime);
        scrollPending = true;
        ResetSheetTime();
        if (sheetOpen)
            sheetFocusPending = true;
    }

    /// <summary>A time from the list goes on the value's day (today when there is none) and closes the popup.</summary>
    private async Task PickTimeAsync(TimeOnly time)
    {
        await CommitAsync(Clamp(ListDate.ToDateTime(time)));
        ClosePopup();
        CloseSheet();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // @bind protects typed text from a slower render. Nothing commits until change; an open popup follows the typed value.
    private void HandleInputAfter()
    {
        currentText ??= "";
        if (popupOpen && formatter.TryParse(currentText, out var typed) && typed is DateTime d)
        {
            calendar?.GoTo(DateOnly.FromDateTime(d));
            times = BuildTimes(DateOnly.FromDateTime(d));
            activeTime = NearestTime(TimeOnly.FromDateTime(d));
            scrollPending = true;
        }
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
            }
            if (pane == DateTimePane.Date)
            {
                if (e.Key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown" or "PageUp" or "PageDown" or "Enter" && calendar is not null)
                    await calendar.HandleKeyAsync(e);
                return;
            }
            int? next = e.Key switch
            {
                "ArrowDown" => activeTime + 1,
                "ArrowUp"   => activeTime - 1,
                "PageDown"  => activeTime + TimePageSize,
                "PageUp"    => activeTime - TimePageSize,
                _           => null
            };
            if (next is int n && times.Count > 0)
            {
                activeTime = Math.Clamp(n, 0, times.Count - 1);
                scrollPending = true;
            }
            else if (e.Key == "ArrowLeft")
                pane = DateTimePane.Date;
            else if (e.Key == "Enter" && activeTime >= 0 && activeTime < times.Count)
                await PickTimeAsync(times[activeTime]);
            return;
        }
        switch (e.Key)
        {
            case "ArrowDown":
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

    private async Task HandleFocus(FocusEventArgs e)
    {
        hasFocus = true;
        // nx-input.js has already swapped in the edit text; only before it has loaded is that done here.
        if (!editOnFocusApplied && Editing && currentText == formatter.Display(current))
            currentText = formatter.Edit(current);
        await OnFocus.InvokeAsync(e);
    }

    private async Task HandleBlur(FocusEventArgs e)
    {
        hasFocus = false;
        ClosePopup();
        await CommitTextAsync();
        currentText = formatter.Display(current);
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
