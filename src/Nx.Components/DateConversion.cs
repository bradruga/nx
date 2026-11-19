using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Converts a picker's bound <typeparamref name="TValue"/> to and from <see cref="DateOnly"/>.
/// Supports <see cref="DateTime"/>, <see cref="DateTimeOffset"/> and <see cref="DateOnly"/>, nullable or not.
/// </summary>
internal static class DateConversion<TValue>
{
    private static readonly Type Underlying = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    public static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    public static readonly bool IsOffset = Underlying == typeof(DateTimeOffset);
    public static readonly bool IsSupported = Underlying == typeof(DateTime) || Underlying == typeof(DateOnly) || IsOffset;

    public static string Unsupported(string component) =>
        $"{component} supports DateTime, DateTimeOffset and DateOnly, nullable or not; not {typeof(TValue).Name}.";

    /// <summary>The date part as shown (a <see cref="DateTimeOffset"/>'s own local date); <c>null</c> for null. <c>default(DateTime)</c> for a non-nullable type is a date like any other.</summary>
    public static DateOnly? ToDate(TValue? value) => value switch
    {
        DateTime d       => DateOnly.FromDateTime(d),
        DateTimeOffset d => DateOnly.FromDateTime(d.DateTime),
        DateOnly d       => d,
        _                => null
    };

    /// <summary>
    /// A value for <paramref name="date"/>. A <see cref="DateTime"/> keeps the time of day and kind of <paramref name="previous"/>;
    /// a <see cref="DateTimeOffset"/> its time of day and offset, else the offset of <paramref name="zone"/> on that date.
    /// </summary>
    public static TValue? FromDate(DateOnly? date, TValue? previous, BrowserTimeZone zone)
    {
        if (date is not DateOnly d)
            return default;
        if (Underlying == typeof(DateOnly))
            return (TValue)(object)d;
        if (IsOffset)
        {
            var at = previous is DateTimeOffset o ? TimeOnly.FromDateTime(o.DateTime) : TimeOnly.MinValue;
            return (TValue)(object)zone.At(d.ToDateTime(at), previous is DateTimeOffset p ? p : null);
        }
        var time = previous is DateTime t ? TimeOnly.FromDateTime(t) : TimeOnly.MinValue;
        var kind = previous is DateTime k ? k.Kind : DateTimeKind.Unspecified;
        return (TValue)(object)DateTime.SpecifyKind(d.ToDateTime(time), kind);
    }
}

/// <summary>What nx-picker.js <c>timeZone</c> answers: the IANA name and the offset now, in minutes east of UTC.</summary>
internal sealed record BrowserZone(string? Id, int Offset);

/// <summary>
/// The offset a picker gives a new <see cref="DateTimeOffset"/>: the browser's time zone on that date, once nx-picker.js
/// has answered; the server's until then (prerender).
/// </summary>
internal sealed class BrowserTimeZone
{
    private TimeZoneInfo? zone;
    private TimeSpan? offsetNow;    // used when the server does not know the browser's zone by name

    private readonly TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Asks the browser for its zone.</summary>
    public async Task LoadAsync(IJSObjectReference module)
    {
        try { Set(await module.InvokeAsync<BrowserZone?>("timeZone")); }
        finally { loaded.TrySetResult(); }
    }

    /// <summary>
    /// Waits for the browser's answer (three seconds at most), so a value committed right after the page loads does not
    /// get the server's offset.
    /// </summary>
    public Task WhenLoadedAsync() =>
        loaded.Task.IsCompleted ? Task.CompletedTask : Task.WhenAny(loaded.Task, Task.Delay(3000));

    public void Set(BrowserZone? browser)
    {
        if (browser is null)
            return;
        offsetNow = TimeSpan.FromMinutes(browser.Offset);
        if (!string.IsNullOrEmpty(browser.Id) && TimeZoneInfo.TryFindSystemTimeZoneById(browser.Id, out var found))
            zone = found;
    }

    /// <summary>Today's date in the browser's zone.</summary>
    public DateOnly Today => DateOnly.FromDateTime(
        zone is not null ? TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime
        : offsetNow is TimeSpan o ? DateTimeOffset.UtcNow.ToOffset(o).DateTime
        : DateTime.Today);

    /// <summary>The zone's offset at a local date and time, to the minute (daylight saving as on that date).</summary>
    public TimeSpan OffsetAt(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = zone?.GetUtcOffset(local) ?? offsetNow ?? TimeZoneInfo.Local.GetUtcOffset(local);
        return TimeSpan.FromMinutes(Math.Round(offset.TotalMinutes));
    }

    /// <summary>
    /// <paramref name="local"/> with the offset of <paramref name="previous"/>, else the zone's. A non-nullable field's
    /// <c>default</c> (year 1, +00:00) counts as no previous value.
    /// </summary>
    public DateTimeOffset At(DateTime local, DateTimeOffset? previous)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = previous is DateTimeOffset p && p != default ? p.Offset : OffsetAt(local);
        // The first and last hours of the calendar cannot carry every offset.
        try { return new DateTimeOffset(local, offset); }
        catch (ArgumentOutOfRangeException) { return new DateTimeOffset(local, TimeSpan.Zero); }
    }
}
