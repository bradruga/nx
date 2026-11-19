using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Hour, minute and AM/PM columns for picking a time on a phone: the body of <see cref="NxTimePicker{TValue}"/>'s and
/// <see cref="NxDateTimePicker{TValue}"/>'s sheet. A tap sets that part of <see cref="Value"/>; times outside
/// <see cref="Min"/> and <see cref="Max"/> are disabled. Public only because Razor components cannot be internal.
/// </summary>
public partial class NxTimeColumns : IAsyncDisposable
{
    private const string ModulePath = "./_content/Nx.Components/nx-picker.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? module;
    private ElementReference root;
    private int? openedMinute;      // a minute off the grid stays listed while the columns are shown
    private bool revealPending;

    /// <summary>The time shown as selected. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TimeOnly Value { get; set; }

    /// <summary>Raised when an hour, a minute or AM/PM is picked, with the new time moved inside <see cref="Min"/> and <see cref="Max"/>.</summary>
    [Parameter] public EventCallback<TimeOnly> ValueChanged { get; set; }

    /// <summary>Earliest time that can be picked.</summary>
    [Parameter] public TimeOnly? Min { get; set; }

    /// <summary>Latest time that can be picked.</summary>
    [Parameter] public TimeOnly? Max { get; set; }

    /// <summary><c>true</c> for hours 00–23; <c>false</c> for 12–11 with an AM/PM column.</summary>
    [Parameter] public bool Use24Hour { get; set; }

    /// <summary>Minutes between the rows of the minute column, 1 to 30. Default: <c>5</c>.</summary>
    [Parameter] public int MinuteStep { get; set; } = 5;

    /// <summary>Culture for the AM/PM designators. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Accessible name of the group. Default: "Time".</summary>
    [Parameter] public string AriaLabel { get; set; } = "Time";

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    private TimeOnly Lo => Min ?? TimeOnly.MinValue;
    private TimeOnly Hi => Max ?? new TimeOnly(23, 59);
    private int Step => Math.Clamp(MinuteStep, 1, 30);
    private DateTimeFormatInfo Names => (Culture ?? CultureInfo.CurrentCulture).DateTimeFormat;
    private string AmText => Names.AMDesignator is { Length: > 0 } am ? am : "AM";
    private string PmText => Names.PMDesignator is { Length: > 0 } pm ? pm : "PM";

    /// <summary>00–23, or the twelve hours of the value's half of the day, starting at 12.</summary>
    private IEnumerable<int> Hours => Use24Hour
        ? Enumerable.Range(0, 24)
        : Enumerable.Range(0, 12).Select(h => h + (Value.Hour >= 12 ? 12 : 0));

    private IEnumerable<int> Minutes => Enumerable.Range(0, 60).Where(m => m % Step == 0 || m == openedMinute);

    private string HourText(int hour) => Use24Hour ? hour.ToString("00") : (hour % 12 == 0 ? 12 : hour % 12).ToString();

    private bool HourDisabled(int hour) => new TimeOnly(hour, 59) < Lo || new TimeOnly(hour, 0) > Hi;

    private bool InRange(TimeOnly time) => time >= Lo && time <= Hi;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Value.Minute % Step != 0)
            openedMinute = Value.Minute;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("initColumns", root);
        }
        else if (revealPending && module is not null)
        {
            revealPending = false;
            await module.InvokeVoidAsync("revealColumns", root);
        }
    }

    /// <summary>A pick that leaves the range (hour 6 at :15 with a Min of 6:30) moves to the nearest limit.</summary>
    private async Task PickAsync(TimeOnly time)
    {
        if (time < Lo) time = Lo;
        if (time > Hi) time = Hi;
        if (time == Value)
            return;
        revealPending = true;
        await ValueChanged.InvokeAsync(time);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
    }
}
