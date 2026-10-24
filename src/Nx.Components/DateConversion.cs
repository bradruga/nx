namespace Nx.Components;

/// <summary>
/// Converts a picker's bound <typeparamref name="TValue"/> to and from <see cref="DateOnly"/>.
/// Supports <see cref="DateTime"/> and <see cref="DateOnly"/>, nullable or not.
/// </summary>
internal static class DateConversion<TValue>
{
    private static readonly Type Underlying = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    public static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    public static readonly bool IsSupported = Underlying == typeof(DateTime) || Underlying == typeof(DateOnly);

    public static string Unsupported(string component) =>
        $"{component} supports DateTime and DateOnly, nullable or not; not {typeof(TValue).Name}.";

    /// <summary>The date part; <c>null</c> for null. <c>default(DateTime)</c> for a non-nullable type is a date like any other.</summary>
    public static DateOnly? ToDate(TValue? value) => value switch
    {
        DateTime d => DateOnly.FromDateTime(d),
        DateOnly d => d,
        _          => null
    };

    /// <summary>A value for <paramref name="date"/>. A <see cref="DateTime"/> keeps the time of day and kind of <paramref name="previous"/>.</summary>
    public static TValue? FromDate(DateOnly? date, TValue? previous)
    {
        if (date is not DateOnly d)
            return default;
        if (Underlying == typeof(DateOnly))
            return (TValue)(object)d;
        var time = previous is DateTime p ? TimeOnly.FromDateTime(p) : TimeOnly.MinValue;
        var kind = previous is DateTime k ? k.Kind : DateTimeKind.Unspecified;
        return (TValue)(object)DateTime.SpecifyKind(d.ToDateTime(time), kind);
    }
}
