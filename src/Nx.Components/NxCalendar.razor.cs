using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A month calendar with year and decade views, min/max and disabled dates, week numbers, a Today
/// button and full keyboard navigation. The popup of <see cref="NxDatePicker{TValue}"/> and
/// <see cref="NxDateRangePicker{TValue}"/>; also usable on its own. Bind with <c>@bind-Value</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxCalendar @bind-Value="pourDate" Min="DateOnly.FromDateTime(DateTime.Today)" ShowWeekNumbers="true" /&gt;
/// </code>
/// </example>
public partial class NxCalendar : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // When the calendar takes focus itself (Focusable), nx-input.js cancels the page scroll and
    // button presses of its navigation keys. Navigation itself is C#.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";
    private static readonly string[] NavigationKeys =
        ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End", "Enter", " "];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-calendar-{Guid.NewGuid():N}"[..20];
    private IJSObjectReference? module;
    private ElementReference grids;
    private DateOnly focused;
    private bool keyMoved;              // a key has moved the focused cell: a picker's calendar shows it from then on
    private DateOnly display;           // the first of the first month shown
    private NxCalendarView view;
    private DateOnly? lastValue;
    private bool initialized;

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The selected date. Use <c>@bind-Value</c>.</summary>
    [Parameter] public DateOnly? Value { get; set; }

    /// <summary>Raised when the user picks a date.</summary>
    [Parameter] public EventCallback<DateOnly?> ValueChanged { get; set; }

    /// <summary>Start of a range to highlight, as <see cref="NxDateRangePicker{TValue}"/> does.</summary>
    [Parameter] public DateOnly? RangeStart { get; set; }

    /// <summary>End of a range to highlight.</summary>
    [Parameter] public DateOnly? RangeEnd { get; set; }

    // ── Limits ────────────────────────────────────────────────────────────────

    /// <summary>Earliest date that can be picked; navigation stops there.</summary>
    [Parameter] public DateOnly? Min { get; set; }

    /// <summary>Latest date that can be picked; navigation stops there.</summary>
    [Parameter] public DateOnly? Max { get; set; }

    /// <summary>Returns <c>true</c> for dates that cannot be picked, e.g. weekends or holidays. They stay focusable but are greyed out.</summary>
    [Parameter] public Func<DateOnly, bool>? IsDateDisabled { get; set; }

    // ── Display ───────────────────────────────────────────────────────────────

    /// <summary>How many months show side by side in the month view: 1 (default) or 2.</summary>
    [Parameter] public int Months { get; set; } = 1;

    /// <summary>Shows the week number before each week.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>The first column's day. Default: the culture's first day of the week.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>Culture for month and day names and week numbering. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Shows a Today button under the grid. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowTodayButton { get; set; } = true;

    /// <summary>Extra content in the footer, after the Today button.</summary>
    [Parameter] public RenderFragment? FooterContent { get; set; }

    /// <summary>
    /// Whether the calendar takes keyboard focus and handles its own keys. Default: <c>true</c>. A picker sets
    /// <c>false</c>, keeps focus in its input and forwards keys with <see cref="HandleKeyAsync"/>.
    /// </summary>
    [Parameter] public bool Focusable { get; set; } = true;

    /// <summary>Disables the whole calendar.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Raised after the active (keyboard) cell or the view changes.</summary>
    [Parameter] public EventCallback OnNavigate { get; set; }

    // ── Attributes ────────────────────────────────────────────────────────────

    /// <summary>The grid container's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Accessible name of the grid. Default: the month shown.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    // ── Public API for pickers ────────────────────────────────────────────────

    /// <summary>The id of the grid container, for a picker input's <c>aria-controls</c>.</summary>
    public string GridId => Id ?? generatedId;

    /// <summary>The id of the active cell, for a picker input's <c>aria-activedescendant</c>.</summary>
    public string ActiveDescendant => CellId(focused, view);

    /// <summary>The current view.</summary>
    public NxCalendarView View => view;

    /// <summary>Moves keyboard focus to the grid.</summary>
    public ValueTask FocusAsync() => grids.FocusAsync();

    /// <summary>Shows the month view at <paramref name="date"/> (clamped to Min/Max) and makes it the active cell.</summary>
    public void GoTo(DateOnly date)
    {
        view = NxCalendarView.Month;
        MoveTo(date);
        StateHasChanged();
    }

    /// <summary>
    /// Handles a navigation key as the grid itself would: arrows, Page Up/Down (with Shift, a year), Home/End,
    /// Enter/Space to pick, Ctrl+Up/Down to change view. Returns <c>false</c> for keys it does not use.
    /// </summary>
    public async Task<bool> HandleKeyAsync(KeyboardEventArgs e)
    {
        if (Disabled)
            return false;
        if (e.CtrlKey && e.Key is "ArrowUp" or "ArrowDown")
        {
            if (e.Key == "ArrowUp")
                ZoomOut();
            else
                ZoomIn();
            await NavigatedAsync();
            return true;
        }

        var row = view == NxCalendarView.Month ? 7 : 4;
        DateOnly? target = e.Key switch
        {
            "ArrowLeft"  => Shift(-1),
            "ArrowRight" => Shift(+1),
            "ArrowUp"    => Shift(-row),
            "ArrowDown"  => Shift(+row),
            "PageUp"     => Page(-1, e.ShiftKey),
            "PageDown"   => Page(+1, e.ShiftKey),
            "Home"       => RowEdge(start: true),
            "End"        => RowEdge(start: false),
            _            => null
        };
        if (target is DateOnly t)
        {
            keyMoved = true;
            MoveTo(t);
            await NavigatedAsync();
            return true;
        }
        if (e.Key is "Enter" or " ")
        {
            if (view == NxCalendarView.Month)
                await PickAsync(focused);
            else
            {
                ZoomIn();
                await NavigatedAsync();
            }
            return true;
        }
        return false;
    }

    // ── Dates ─────────────────────────────────────────────────────────────────

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private DayOfWeek WeekStart => FirstDayOfWeek ?? EffectiveCulture.DateTimeFormat.FirstDayOfWeek;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private DateOnly Lowest => Min ?? DateOnly.MinValue;
    private DateOnly Highest => Max is DateOnly max && max >= Lowest ? max : DateOnly.MaxValue;
    private int MonthCount => view == NxCalendarView.Month && Months >= 2 ? 2 : 1;

    private DateOnly Clamp(DateOnly date) => date < Lowest ? Lowest : date > Highest ? Highest : date;

    private static DateOnly AddDaysSafe(DateOnly date, int days)
    {
        var number = Math.Clamp((long)date.DayNumber + days, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber);
        return DateOnly.FromDayNumber((int)number);
    }

    private static DateOnly AddMonthsSafe(DateOnly date, int months)
    {
        var index = Math.Clamp(date.Year * 12L + date.Month - 1 + months, 12L, 9999 * 12L + 11);
        var year = (int)(index / 12);
        var month = (int)(index % 12) + 1;
        return new DateOnly(year, month, Math.Min(date.Day, DateTime.DaysInMonth(year, month)));
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
    private static int DecadeStart(int year) => year - year % 10;

    /// <summary>Outside Min/Max or turned off by <see cref="IsDateDisabled"/>.</summary>
    public bool IsDisabled(DateOnly date) => Disabled || date < Lowest || date > Highest || (IsDateDisabled?.Invoke(date) ?? false);

    private bool MonthDisabled(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddDays(DateTime.DaysInMonth(year, month) - 1);
        return Disabled || last < Lowest || first > Highest;
    }

    private bool YearDisabled(int year) => Disabled || year < Lowest.Year || year > Highest.Year;

    private DateOnly Shift(int cells) => view switch
    {
        NxCalendarView.Month => AddDaysSafe(focused, cells),
        NxCalendarView.Year  => AddMonthsSafe(focused, cells),
        _                    => AddMonthsSafe(focused, cells * 12)
    };

    private DateOnly Page(int direction, bool shift) => view switch
    {
        NxCalendarView.Month => AddMonthsSafe(focused, direction * (shift ? 12 : 1)),
        NxCalendarView.Year  => AddMonthsSafe(focused, direction * 12),
        _                    => AddMonthsSafe(focused, direction * 120)
    };

    private DateOnly RowEdge(bool start)
    {
        switch (view)
        {
            case NxCalendarView.Month:
                var offset = ((int)focused.DayOfWeek - (int)WeekStart + 7) % 7;
                return AddDaysSafe(focused, start ? -offset : 6 - offset);
            case NxCalendarView.Year:
                return AddMonthsSafe(focused, (start ? 1 : 12) - focused.Month);
            default:
                var decade = DecadeStart(focused.Year);
                return AddMonthsSafe(focused, ((start ? decade : decade + 9) - focused.Year) * 12);
        }
    }

    /// <summary>Makes <paramref name="date"/> the active cell and scrolls the months so it shows.</summary>
    private void MoveTo(DateOnly date)
    {
        focused = Clamp(date);
        var month = FirstOfMonth(focused);
        if (month < display)
            display = month;
        else if (month > AddMonthsSafe(display, MonthCount - 1))
            display = AddMonthsSafe(month, -(MonthCount - 1));
    }

    private bool Shows(DateOnly date) => FirstOfMonth(date) >= display && FirstOfMonth(date) <= AddMonthsSafe(display, MonthCount - 1);

    private void ShowAround(DateOnly date)
    {
        focused = Clamp(date);
        display = FirstOfMonth(focused);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!initialized || Value != lastValue)
        {
            lastValue = Value;
            var anchor = Value ?? RangeStart ?? Today;
            // A new value in the months already shown keeps them still.
            if (initialized && view == NxCalendarView.Month && Shows(anchor))
                focused = Clamp(anchor);
            else
                ShowAround(anchor);
            initialized = true;
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
        if (firstRender && Focusable)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("preventKeys", grids, NavigationKeys);
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task PickAsync(DateOnly date)
    {
        if (IsDisabled(date))
            return;
        MoveTo(date);
        lastValue = date;
        await ValueChanged.InvokeAsync(date);
        await NavigatedAsync();
    }

    private async Task PickMonthAsync(int year, int month)
    {
        if (MonthDisabled(year, month))
            return;
        focused = Clamp(new DateOnly(year, month, Math.Min(focused.Day, DateTime.DaysInMonth(year, month))));
        ZoomIn();
        await NavigatedAsync();
    }

    private async Task PickYearAsync(int year)
    {
        if (YearDisabled(year))
            return;
        focused = Clamp(new DateOnly(year, focused.Month, Math.Min(focused.Day, DateTime.DaysInMonth(year, focused.Month))));
        ZoomIn();
        await NavigatedAsync();
    }

    private void ZoomOut()
    {
        if (view != NxCalendarView.Decade)
            view++;
    }

    private void ZoomIn()
    {
        if (view == NxCalendarView.Month)
            return;
        view--;
        if (view == NxCalendarView.Month)
            ShowAround(focused);
    }

    private async Task HandleTitleClick()
    {
        ZoomOut();
        await NavigatedAsync();
    }

    private async Task HandlePrevNext(int direction)
    {
        if (view == NxCalendarView.Month)
        {
            display = AddMonthsSafe(display, direction);
            MoveTo(AddMonthsSafe(focused, direction));
        }
        else
            focused = Clamp(AddMonthsSafe(focused, direction * (view == NxCalendarView.Year ? 12 : 120)));
        await NavigatedAsync();
    }

    private async Task HandleTodayClick()
    {
        view = NxCalendarView.Month;
        ShowAround(Today);
        await PickAsync(Today);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Focusable)
            await HandleKeyAsync(e);
    }

    private async Task NavigatedAsync()
    {
        StateHasChanged();
        await OnNavigate.InvokeAsync();
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string TitleId => $"{GridId}-title";

    private string CellId(DateOnly date, NxCalendarView v) => v switch
    {
        NxCalendarView.Month => $"{GridId}-d{date:yyyyMMdd}",
        NxCalendarView.Year  => $"{GridId}-m{date:yyyyMM}",
        _                    => $"{GridId}-y{date:yyyy}"
    };

    private string MonthTitle(DateOnly month) => month.ToString(EffectiveCulture.DateTimeFormat.YearMonthPattern, EffectiveCulture);

    private string TitleText => view switch
    {
        NxCalendarView.Month when MonthCount == 2 => $"{MonthTitle(display)} – {MonthTitle(AddMonthsSafe(display, 1))}",
        NxCalendarView.Month => MonthTitle(display),
        NxCalendarView.Year  => focused.Year.ToString(EffectiveCulture),
        _                    => $"{DecadeStart(focused.Year)}–{DecadeStart(focused.Year) + 9}"
    };

    private string PrevLabel => view switch
    {
        NxCalendarView.Month => "Previous month",
        NxCalendarView.Year  => "Previous year",
        _                    => "Previous decade"
    };

    private string NextLabel => view switch
    {
        NxCalendarView.Month => "Next month",
        NxCalendarView.Year  => "Next year",
        _                    => "Next decade"
    };

    private bool PrevDisabled => Disabled || view switch
    {
        NxCalendarView.Month => display <= FirstOfMonth(Lowest),
        NxCalendarView.Year  => focused.Year <= Lowest.Year,
        _                    => DecadeStart(focused.Year) <= DecadeStart(Lowest.Year)
    };

    private bool NextDisabled => Disabled || view switch
    {
        NxCalendarView.Month => AddMonthsSafe(display, MonthCount - 1) >= FirstOfMonth(Highest),
        NxCalendarView.Year  => focused.Year >= Highest.Year,
        _                    => DecadeStart(focused.Year) >= DecadeStart(Highest.Year)
    };

    private string? TabIndex => Focusable ? null : "-1";

    /// <summary>The 42 days (six weeks) of the grid for <paramref name="month"/>.</summary>
    private IEnumerable<DateOnly[]> Weeks(DateOnly month)
    {
        var offset = ((int)month.DayOfWeek - (int)WeekStart + 7) % 7;
        var start = AddDaysSafe(month, -offset);
        for (var w = 0; w < 6; w++)
            yield return Enumerable.Range(0, 7).Select(d => AddDaysSafe(start, w * 7 + d)).ToArray();
    }

    private IEnumerable<DayOfWeek> WeekDays => Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)WeekStart + i) % 7));

    /// <summary>ISO 8601 when the culture counts weeks that way (Monday, first four-day week), else the culture's own rule.</summary>
    private int WeekNumber(DateOnly[] week)
    {
        var format = EffectiveCulture.DateTimeFormat;
        if (WeekStart == DayOfWeek.Monday && format.CalendarWeekRule == CalendarWeekRule.FirstFourDayWeek)
            return ISOWeek.GetWeekOfYear(week[3].ToDateTime(TimeOnly.MinValue));
        return EffectiveCulture.Calendar.GetWeekOfYear(week[6].ToDateTime(TimeOnly.MinValue), format.CalendarWeekRule, WeekStart);
    }

    private bool InRange(DateOnly date) => RangeStart is DateOnly s && RangeEnd is DateOnly e && date > s && date < e;

    private string DayClass(DateOnly date, DateOnly month)
    {
        var classes = new List<string> { "nx-calendar-cell" };
        if (date.Month != month.Month || date.Year != month.Year) classes.Add("nx-calendar-other");
        if (date == Today) classes.Add("nx-calendar-today");
        if (date == Value || date == RangeStart || date == RangeEnd) classes.Add("nx-calendar-selected");
        if (date == RangeStart && RangeEnd is not null && RangeEnd != RangeStart) classes.Add("nx-calendar-range-start");
        if (date == RangeEnd && RangeStart is not null && RangeEnd != RangeStart) classes.Add("nx-calendar-range-end");
        if (InRange(date)) classes.Add("nx-calendar-in-range");
        if (date == focused) classes.Add("nx-calendar-focused");
        if (IsDisabled(date)) classes.Add("nx-calendar-disabled");
        return string.Join(' ', classes);
    }

    private string CellClass(bool selected, bool isFocused, bool disabled, bool other, bool current) => string.Join(' ', new[]
    {
        "nx-calendar-cell",
        other      ? "nx-calendar-other" : null,
        current    ? "nx-calendar-today" : null,
        selected   ? "nx-calendar-selected" : null,
        isFocused  ? "nx-calendar-focused" : null,
        disabled   ? "nx-calendar-disabled" : null
    }.Where(c => c is not null));

    private string RootClass => string.Join(' ', new[]
    {
        "nx-calendar",
        MonthCount == 2 ? "nx-calendar-two-months" : null,
        Focusable ? "nx-calendar-focusable" : null,
        keyMoved ? "nx-calendar-keyboard" : null,
        Disabled ? "nx-calendar-is-disabled" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", grids);
            await module.DisposeAsync();
        }
        catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
