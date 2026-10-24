namespace Nx.Components;

/// <summary>
/// Converts an <see cref="NxTimePicker{TValue}"/>'s bound <typeparamref name="TValue"/> to and from <see cref="TimeOnly"/>.
/// Supports <see cref="TimeOnly"/>, <see cref="TimeSpan"/> and <see cref="DateTime"/>, nullable or not.
/// </summary>
internal static class TimeConversion<TValue>
{
    private static readonly Type Underlying = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    public static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    public static readonly bool IsSupported =
        Underlying == typeof(TimeOnly) || Underlying == typeof(TimeSpan) || Underlying == typeof(DateTime);

    public static TimeOnly? ToTime(TValue? value) => value switch
    {
        TimeOnly t => t,
        TimeSpan t => TimeOnly.FromTimeSpan(new TimeSpan(Math.Clamp(t.Ticks, 0, TimeSpan.TicksPerDay - 1))),
        DateTime d => TimeOnly.FromDateTime(d),
        _          => null
    };

    /// <summary>A value for <paramref name="time"/>. A <see cref="DateTime"/> keeps the date and kind of <paramref name="previous"/>, else today.</summary>
    public static TValue? FromTime(TimeOnly? time, TValue? previous)
    {
        if (time is not TimeOnly t)
            return default;
        if (Underlying == typeof(TimeOnly))
            return (TValue)(object)t;
        if (Underlying == typeof(TimeSpan))
            return (TValue)(object)t.ToTimeSpan();
        var date = previous is DateTime p ? DateOnly.FromDateTime(p) : DateOnly.FromDateTime(DateTime.Today);
        var kind = previous is DateTime k ? k.Kind : DateTimeKind.Unspecified;
        return (TValue)(object)DateTime.SpecifyKind(date.ToDateTime(t), kind);
    }
}
