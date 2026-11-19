using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A time input for <see cref="TimeOnly"/>, <see cref="TimeSpan"/>, <see cref="DateTime"/> or <see cref="DateTimeOffset"/>,
/// nullable or not: typed entry in 12- or 24-hour form, a list of times every <see cref="Step"/> minutes, min/max, and
/// the same label and clear options as <see cref="NxTextBox"/>. On a phone it opens as a full-screen sheet with hour
/// and minute columns (<see cref="NxTimeColumns"/>) and Done.
/// </summary>
/// <typeparam name="TValue">The bound time type. A <see cref="DateTime"/> keeps its date; a <see cref="DateTimeOffset"/> its offset too, and a new one takes the browser's.</typeparam>
/// <example>
/// <code>
/// &lt;NxTimePicker Label="Truck arrives" @bind-Value="load.Arrival" Step="15" Min="new TimeOnly(6, 0)" /&gt;
/// </code>
/// </example>
public partial class NxTimePicker<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // As NxDatePicker, through nx-picker.js: the 24-hour mask, popup placement, the list keys' browser
    // defaults, the sheet's scroll lock, and scrolling the active time into view.

    private const string ModulePath = "./_content/Nx.Components/nx-picker.js";
    private const int PopupMaxHeight = 280;

    [GeneratedRegex(@"^\s*(\d{1,2})(?:\D?(\d{2}))?\s*([ap])?\.?\s*m?\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex LooseTime();

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly BrowserTimeZone zone = new();
    private readonly string generatedId = $"nx-timepicker-{Guid.NewGuid():N}"[..23];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxTimePicker<TValue>>? selfReference;
    private ElementReference input;
    private ElementReference toggle;
    private ElementReference popup;
    private ElementReference list;
    private List<TimeOnly> times = [];
    private string currentText = "";
    private TimeOnly? current;
    private TimeOnly sheetTime;         // what the sheet's columns show; Done commits it
    private int activeIndex = -1;
    private string? appliedMask;
    private bool hasFocus;
    private bool typedSinceNavigate;    // Enter takes the typed text rather than the highlight
    private bool isNarrow;
    private bool popupOpen, popupPlaced, closePending, scrollPending;
    private bool sheetOpen, scrollLockPending, returnFocusPending;

    /// <summary>Creates the component.</summary>
    public NxTimePicker() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The time. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the value is committed: on picking a time, on Enter, on leaving the field.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Earliest allowed time; the list starts there. Typed times before it are moved up to it.</summary>
    [Parameter] public TimeOnly? Min { get; set; }

    /// <summary>Latest allowed time; the list ends there. Typed times after it are moved back to it.</summary>
    [Parameter] public TimeOnly? Max { get; set; }

    /// <summary>Minutes between the times in the list. Typed times need not be on a step; the phone sheet's minute column lists every 5 minutes, or every <c>Step</c> under 5. Default: <c>30</c>.</summary>
    [Parameter] public int Step { get; set; } = 30;

    // ── Format ────────────────────────────────────────────────────────────────

    /// <summary><c>true</c> for 24-hour (<c>14:30</c>), <c>false</c> for 12-hour (<c>2:30 PM</c>). Default: the culture's short time.</summary>
    [Parameter] public bool? Use24Hour { get; set; }

    /// <summary>Culture for the AM/PM designators and separators. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    // ── Label and text ────────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Placeholder text shown while the field is empty. Default: <c>hh:mm</c>, with <c>AM/PM</c> for 12-hour.</summary>
    [Parameter] public string? Placeholder { get; set; }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>Shows a clear button while there is a time. Nullable types only.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute; the list does not open.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the input after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

    /// <summary>When the picker opens as a full-screen sheet of hour and minute columns instead of the list. Default: <see cref="NxMobileMode.Auto"/>.</summary>
    [Parameter] public NxMobileMode MobileMode { get; set; } = NxMobileMode.Auto;

    /// <summary>With <see cref="NxMobileMode.Auto"/>, the viewport width in pixels at or below which the sheet is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The sheet's title. Default: <see cref="Label"/>, else "Choose a time".</summary>
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

    /// <summary>Raised when the input loses focus, after the typed time is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Text ──────────────────────────────────────────────────────────────────

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private bool Is24Hour => Use24Hour ?? !EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains('h');
    private string Pattern => Is24Hour ? "HH:mm" : "h:mm tt";
    private string? Mask => Is24Hour ? DateText.Fixed(Pattern, EffectiveCulture)?.Mask : null;

    private string FormatDisplay(TimeOnly? time) => time?.ToString(Pattern, EffectiveCulture) ?? "";

    /// <summary>
    /// Reads typed or pasted text: the display pattern, anything the culture reads as a time, then loose forms
    /// such as <c>930</c>, <c>9:30p</c> or <c>1430</c>.
    /// </summary>
    private bool TryParse(string text, out TimeOnly? time)
    {
        time = null;
        text = text.Trim();
        if (text.Length == 0)
            return true;
        if (TimeOnly.TryParseExact(text, Pattern, EffectiveCulture, DateTimeStyles.AllowWhiteSpaces, out var exact) ||
            TimeOnly.TryParse(text, EffectiveCulture, DateTimeStyles.AllowWhiteSpaces, out exact))
        {
            time = exact;
            return true;
        }
        var match = LooseTime().Match(text);
        if (!match.Success)
            return false;
        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (match.Groups[3].Success)
        {
            if (hour is < 1 or > 12)
                return false;
            var pm = char.ToLowerInvariant(match.Groups[3].Value[0]) == 'p';
            hour = hour % 12 + (pm ? 12 : 0);
        }
        if (hour > 23 || minute > 59)
            return false;
        time = new TimeOnly(hour, minute);
        return true;
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private string ListId => $"{InputId}-list";
    private string OptionId(int index) => $"{InputId}-option-{index}";
    private bool Editable => !Disabled && !ReadOnly;
    private bool ShowClear => Clearable && TimeConversion<TValue>.IsNullable && Editable && current is not null;
    private string SheetTitle => MobileTitle ?? Label ?? "Choose a time";

    private bool UseSheet => MobileMode switch
    {
        NxMobileMode.Always => true,
        NxMobileMode.Never  => false,
        _                   => isNarrow
    };

    private string PlaceholderText => Placeholder ?? (Is24Hour ? "hh:mm" : "hh:mm AM/PM");

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-timepicker-input"
        : $"nx-timepicker-input {validation.CssClass}";

    private string? RootClass => popupOpen || sheetOpen ? $"nx-timepicker-open {Class}".Trim() : Class;

    private string ItemClass(int index, TimeOnly time) => string.Join(' ', new[]
    {
        "nx-timepicker-item",
        index >= 0 && index == activeIndex ? "nx-timepicker-item-active" : null,
        time == current ? "nx-timepicker-item-selected" : null
    }.Where(c => c is not null));

    /// <summary>Every <see cref="Step"/> minutes from Min (else midnight) to Max (else the last step of the day).</summary>
    private List<TimeOnly> BuildTimes()
    {
        var step = Math.Clamp(Step, 1, 720);
        var first = (int)(Min ?? TimeOnly.MinValue).ToTimeSpan().TotalMinutes;
        var last = Max is TimeOnly max ? (int)max.ToTimeSpan().TotalMinutes : 24 * 60 - 1;
        var result = new List<TimeOnly>();
        for (var m = first; m <= last; m += step)
            result.Add(new TimeOnly(m / 60, m % 60));
        return result;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!TimeConversion<TValue>.IsSupported)
            throw new InvalidOperationException(
                $"NxTimePicker supports TimeOnly, TimeSpan, DateTime and DateTimeOffset, nullable or not; not {typeof(TValue).Name}.");

        validation.Attach(CascadedEditContext, ValueExpression);
        times = BuildTimes();

        var incoming = TimeConversion<TValue>.ToTime(Value);
        if (incoming is TimeOnly t)
            incoming = new TimeOnly(t.Hour, t.Minute);
        if (incoming != current || (!hasFocus && currentText != FormatDisplay(current)))
        {
            current = incoming;
            currentText = FormatDisplay(current);
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
            if (TimeConversion<TValue>.IsOffset)
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

        if (appliedMask != Mask)
        {
            appliedMask = Mask;
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
            await module.InvokeAsync<string>("open", input, popup, list, PopupMaxHeight, null, null);
            scrollPending = true;
        }
        if (popupOpen && scrollPending)
        {
            scrollPending = false;
            await module.InvokeVoidAsync("scrollActive", list, false);
        }
        if (scrollLockPending)
        {
            scrollLockPending = false;
            await module.InvokeVoidAsync("lockScroll", sheetOpen);
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
        if (!Editable || popupOpen || sheetOpen || times.Count == 0)
            return;
        if (UseSheet)
        {
            // The columns open on the value, else on the list's time nearest to now.
            sheetTime = current ?? times[NearestIndex(null)];
            if (Min is TimeOnly min && sheetTime < min) sheetTime = min;
            if (Max is TimeOnly max && sheetTime > max) sheetTime = max;
            sheetOpen = true;
            scrollLockPending = true;
            return;
        }
        popupOpen = true;
        popupPlaced = false;
        typedSinceNavigate = false;
        activeIndex = NearestIndex(TryParse(currentText, out var typed) && typed is not null ? typed : current);
        if (!hasFocus)
            await input.FocusAsync();
    }

    /// <summary>The time at or just after <paramref name="time"/> (else now), so the list opens where the user is.</summary>
    private int NearestIndex(TimeOnly? time)
    {
        var target = time ?? TimeOnly.FromDateTime(DateTime.Now);
        var index = times.FindIndex(t => t >= target);
        return index < 0 ? times.Count - 1 : index;
    }

    private void ClosePopup()
    {
        if (!popupOpen)
            return;
        popupOpen = false;
        activeIndex = -1;
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

    /// <summary>Empty is null (kept as is for non-nullable types); unreadable text reverts; outside Min/Max moves to the limit.</summary>
    private async Task CommitTextAsync()
    {
        if (!TryParse(currentText, out var parsed) || (parsed is null && !TimeConversion<TValue>.IsNullable))
        {
            currentText = FormatDisplay(current);
            return;
        }
        if (parsed is TimeOnly t)
        {
            if (Min is TimeOnly min && t < min) t = min;
            if (Max is TimeOnly max && t > max) t = max;
            parsed = t;
        }
        await CommitAsync(parsed);
    }

    private async Task CommitAsync(TimeOnly? time)
    {
        var changed = time != current;
        current = time;
        currentText = FormatDisplay(time);
        if (!changed)
            return;
        if (TimeConversion<TValue>.IsOffset)
            await zone.WhenLoadedAsync();
        await ValueChanged.InvokeAsync(TimeConversion<TValue>.FromTime(time, Value, zone));
        validation.NotifyFieldChanged();
    }

    private async Task PickAsync(TimeOnly time)
    {
        await CommitAsync(time);
        ClosePopup();
        CloseSheet();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // An open list follows the typed time.
    private void HandleInputAfter()
    {
        currentText ??= "";
        typedSinceNavigate = true;
        if (popupOpen && TryParse(currentText, out var typed) && typed is not null)
        {
            activeIndex = NearestIndex(typed);
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
            var page = Math.Max(1, PopupMaxHeight / 34);
            int? next = e.Key switch
            {
                "ArrowDown" when !e.AltKey => activeIndex + 1,
                "ArrowUp" when !e.AltKey   => activeIndex - 1,
                "PageDown"                 => activeIndex + page,
                "PageUp"                   => activeIndex - page,
                _                          => null
            };
            if (next is int n)
            {
                activeIndex = Math.Clamp(n, 0, times.Count - 1);
                typedSinceNavigate = false;
                scrollPending = true;
                return;
            }
            switch (e.Key)
            {
                case "Enter" when activeIndex >= 0:
                    // Text typed since the last arrow key wins: the highlight only follows it to the nearest step.
                    if (typedSinceNavigate && TryParse(currentText, out var typed) && typed is not null)
                        await CommitTextAsync();
                    else
                        await CommitAsync(times[activeIndex]);
                    ClosePopup();
                    break;
                case "Escape" or "Tab":
                case "ArrowUp" when e.AltKey:
                    ClosePopup();
                    break;
            }
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
        await OnFocus.InvokeAsync(e);
    }

    private async Task HandleBlur(FocusEventArgs e)
    {
        hasFocus = false;
        ClosePopup();
        await CommitTextAsync();
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
