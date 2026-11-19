using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A read-only week calendar: items from <see cref="Data"/> placed by their start and end on seven day columns, side by
/// side where they overlap, split at midnight when they run overnight, with work hours shaded and Today / previous /
/// next week. Fields are read with lambdas. Scoped to what ERP's shift schedule uses: one
/// week view, no creating, editing or deleting.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <example>
/// <code>
/// &lt;NxScheduler Data="appointments" ItemStart="a =&gt; a.Start" ItemEnd="a =&gt; a.End" @bind-Date="week" WorkDayStart="new(7, 0)" WorkDayEnd="new(19, 0)" Height="600px"&gt;
///     &lt;ItemTemplate&gt;@context.ShiftName&lt;/ItemTemplate&gt;
/// &lt;/NxScheduler&gt;
/// </code>
/// </example>
public partial class NxScheduler<TItem>
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-scheduler.js only scrolls the hours to the start of the work day when the calendar first shows.

    private const string ModulePath = "./_content/Nx.Components/nx-scheduler.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference body;

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The items. Each needs a start and an end.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>An item's start; an item without one is not shown. Required.</summary>
    [Parameter, EditorRequired] public Func<TItem, DateTime?>? ItemStart { get; set; }

    /// <summary>An item's end; an item without one is not shown. Required.</summary>
    [Parameter, EditorRequired] public Func<TItem, DateTime?>? ItemEnd { get; set; }

    /// <summary>An item's title. Default: none.</summary>
    [Parameter] public Func<TItem, string?>? ItemTitle { get; set; }

    /// <summary>An item's content in place of its title and time; the context is the item.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    // ── The week ──────────────────────────────────────────────────────────────

    /// <summary>A day of the week shown. Use <c>@bind-Date</c>; Today and the arrows change it. Default: today.</summary>
    [Parameter] public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>Raised when Today or an arrow moves to another week.</summary>
    [Parameter] public EventCallback<DateTime> DateChanged { get; set; }

    /// <summary>The first hour shown each day. Default: <c>00:00</c>.</summary>
    [Parameter] public TimeOnly StartTime { get; set; }

    /// <summary>The last time shown each day, rounded up to a whole slot. Default: <c>23:59</c> (the whole day).</summary>
    [Parameter] public TimeOnly EndTime { get; set; } = new(23, 59);

    /// <summary>The work day's start; the hours outside it are shaded and the calendar opens scrolled to it. Default: <c>08:00</c>.</summary>
    [Parameter] public TimeOnly WorkDayStart { get; set; } = new(8, 0);

    /// <summary>The work day's end. Default: <c>17:00</c>.</summary>
    [Parameter] public TimeOnly WorkDayEnd { get; set; } = new(17, 0);

    /// <summary>Minutes per labelled slot. Default: <c>60</c>.</summary>
    [Parameter] public int SlotDuration { get; set; } = 60;

    /// <summary>Rows each slot is divided into. Default: <c>2</c>.</summary>
    [Parameter] public int SlotDivisions { get; set; } = 2;

    /// <summary>Day and time formats and the first day of the week. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    // ── Look ──────────────────────────────────────────────────────────────────

    /// <summary>The calendar's height; the hours scroll under the day headers. Default: <c>600px</c>.</summary>
    [Parameter] public string? Height { get; set; } = "600px";

    /// <summary>The hours' accessible name. Default: the week's dates.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes go to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ── The grid ──────────────────────────────────────────────────────────────

    private CultureInfo Formats => Culture ?? CultureInfo.CurrentCulture;
    private DateTime Today => DateTime.Today;
    private int SlotDivisionsOrOne => Math.Max(1, SlotDivisions);
    private double RowMinutes => Math.Max(1, SlotDuration) / (double)SlotDivisionsOrOne;
    private string TimeFormat => Formats.DateTimeFormat.ShortTimePattern.Contains('H') ? "HH:mm" : "h tt";

    private DateTime WeekStart
    {
        get
        {
            var first = Formats.DateTimeFormat.FirstDayOfWeek;
            return Date.Date.AddDays(-(((int)Date.DayOfWeek - (int)first + 7) % 7));
        }
    }

    private IEnumerable<DateTime> Days => Enumerable.Range(0, 7).Select(i => WeekStart.AddDays(i));
    private bool IsThisWeek => Today >= WeekStart && Today < WeekStart.AddDays(7);

    // Whole rows from StartTime, enough to reach EndTime.
    private int SlotCount => Math.Max(1, (int)Math.Ceiling(WindowMinutes(EndTime) / RowMinutes));
    private double WindowMinutes(TimeOnly end) => Math.Max(0, (end - StartTime).TotalMinutes);
    private double ShownMinutes => SlotCount * RowMinutes;
    private TimeOnly SlotTime(int slot) => StartTime.AddMinutes(slot * RowMinutes);

    private string RangeText
    {
        get
        {
            var start = WeekStart;
            var end = start.AddDays(6);
            return start.Month == end.Month
                ? $"{start.ToString("MMMM d", Formats)} – {end.ToString("%d", Formats)}, {end.Year}"
                : start.Year == end.Year
                    ? $"{start.ToString("MMM d", Formats)} – {end.ToString("MMM d", Formats)}, {end.Year}"
                    : $"{start.ToString("MMM d, yyyy", Formats)} – {end.ToString("MMM d, yyyy", Formats)}";
        }
    }

    private string SlotClass(int slot)
    {
        var at = SlotTime(slot);
        var css = slot % SlotDivisionsOrOne == 0 ? "nx-scheduler-slot nx-scheduler-slot-major" : "nx-scheduler-slot";
        return at >= WorkDayStart && at < WorkDayEnd ? css : $"{css} nx-scheduler-slot-off";
    }

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-scheduler" : $"nx-scheduler {Class}";
    private string? GridStyle => $"--nx-scheduler-rows: {SlotCount}";

    private string? OuterStyle
    {
        get
        {
            var height = string.IsNullOrEmpty(Height) ? null : $"height: {Height}";
            return string.IsNullOrEmpty(Style) ? height : height is null ? Style : $"{height}; {Style}";
        }
    }

    // ── Placing items ─────────────────────────────────────────────────────────

    private sealed record Placed(TItem Item, string Title, string TimeText, string Tooltip, string Style);

    private sealed class Segment(TItem item, DateTime start, DateTime end, DateTime itemStart, DateTime itemEnd)
    {
        public TItem Item { get; } = item;
        public DateTime Start { get; } = start;
        public DateTime End { get; } = end;
        public DateTime ItemStart { get; } = itemStart;
        public DateTime ItemEnd { get; } = itemEnd;
        public int Column { get; set; }
        public int Columns { get; set; } = 1;
    }

    /// <summary>The day's pieces of every item, clipped to the hours shown, overlaps side by side.</summary>
    private List<Placed> PlaceDay(DateTime day)
    {
        var from = day + StartTime.ToTimeSpan();
        var to = from.AddMinutes(ShownMinutes);
        var segments = new List<Segment>();
        foreach (var item in Data ?? [])
        {
            if (item is null || ItemStart!(item) is not { } start || ItemEnd!(item) is not { } end || end <= start)
                continue;
            var s = start < from ? from : start;
            var e = end > to ? to : end;
            if (s < e)
                segments.Add(new Segment(item, s, e, start, end));
        }
        segments.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));

        // Clusters of overlapping pieces share the width; each piece takes the first column free at its start.
        var cluster = new List<Segment>();
        var columnEnds = new List<DateTime>();
        var clusterEnd = DateTime.MinValue;
        void Close()
        {
            foreach (var segment in cluster)
                segment.Columns = columnEnds.Count;
            cluster.Clear();
            columnEnds.Clear();
        }
        foreach (var segment in segments)
        {
            if (cluster.Count > 0 && segment.Start >= clusterEnd)
                Close();
            var column = columnEnds.FindIndex(endAt => endAt <= segment.Start);
            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(segment.End);
            }
            else
            {
                columnEnds[column] = segment.End;
            }
            segment.Column = column;
            cluster.Add(segment);
            if (cluster.Count == 1 || segment.End > clusterEnd)
                clusterEnd = segment.End;
        }
        Close();

        var total = ShownMinutes;
        return
        [
            .. segments.Select(s =>
            {
                var top = (s.Start - from).TotalMinutes / total * 100;
                var height = (s.End - s.Start).TotalMinutes / total * 100;
                var width = 100.0 / s.Columns;
                var style = string.Create(CultureInfo.InvariantCulture,
                    $"top: {top:0.###}%; height: {height:0.###}%; left: {s.Column * width:0.###}%; width: {width:0.###}%");
                var title = ItemTitle?.Invoke(s.Item) ?? "";
                var endText = s.ItemEnd.Date == s.ItemStart.Date
                    ? s.ItemEnd.ToString("t", Formats)
                    : $"{s.ItemEnd.ToString("ddd", Formats)} {s.ItemEnd.ToString("t", Formats)}";
                var time = $"{s.ItemStart.ToString("t", Formats)} – {endText}";
                var tooltip = string.IsNullOrEmpty(title) ? $"{s.ItemStart.ToString("ddd", Formats)} {time}" : $"{title}, {s.ItemStart.ToString("ddd", Formats)} {time}";
                return new Placed(s.Item, title, time, tooltip, style);
            })
        ];
    }

    // ── Lifecycle and navigation ──────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ItemStart is null || ItemEnd is null)
            throw new InvalidOperationException("NxScheduler needs ItemStart and ItemEnd.");
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        var module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        // Open at the work day, as a paper schedule would.
        var row = Math.Max(0, (WorkDayStart - StartTime).TotalMinutes / RowMinutes);
        await module.InvokeVoidAsync("scrollToRow", body, row);
        await module.DisposeAsync();
    }

    private Task MoveAsync(int days) => SetDateAsync(Date.AddDays(days));

    private Task TodayAsync() => SetDateAsync(Today);

    private async Task SetDateAsync(DateTime date)
    {
        Date = date;
        await DateChanged.InvokeAsync(date);
    }
}
