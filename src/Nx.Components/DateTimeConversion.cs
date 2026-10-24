using System.Globalization;

namespace Nx.Components;

/// <summary>
/// Converts an <see cref="NxDateTimePicker{TValue}"/>'s bound <typeparamref name="TValue"/> to and from <see cref="DateTime"/>.
/// Supports <see cref="DateTime"/>, nullable or not.
/// </summary>
internal static class DateTimeConversion<TValue>
{
    public static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    public static readonly bool IsSupported = (Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)) == typeof(DateTime);

    public static DateTime? ToDateTime(TValue? value) => value is DateTime d ? d : null;

    /// <summary>A value for <paramref name="dateTime"/>, with the <see cref="DateTimeKind"/> of <paramref name="previous"/>.</summary>
    public static TValue? FromDateTime(DateTime? dateTime, TValue? previous)
    {
        if (dateTime is not DateTime d)
            return default;
        var kind = previous is DateTime p ? p.Kind : DateTimeKind.Unspecified;
        return (TValue)(object)DateTime.SpecifyKind(d, kind);
    }
}

/// <summary>A date-time picker's display and edit formats for one Format and culture: formats, parses, masks.</summary>
internal sealed class DateTimeFormatter
{
    private const DateTimeStyles Styles = DateTimeStyles.AllowWhiteSpaces;

    private readonly CultureInfo culture;
    private readonly string display;
    private readonly (string Edit, string Mask)? edit;

    public DateTimeFormatter(string format, CultureInfo culture)
    {
        this.culture = culture;
        display = DateText.Expand(format, culture);
        // Only an all-number, 24-hour format (MM/dd/yyyy HH:mm) is typed behind a mask; others are typed freely.
        edit = DateText.Fixed(display, culture);
    }

    /// <summary>The input mask, e.g. <c>99/99/9999 99:99</c>; <c>null</c> for none.</summary>
    public string? Mask => edit?.Mask;

    /// <summary>The pattern as a hint, e.g. <c>m/d/yyyy h:mm am/pm</c>.</summary>
    public string Hint => (edit?.Edit ?? display)
        .Replace("'", "")
        .Replace("tt", "\u0001")
        .Replace("/", culture.DateTimeFormat.DateSeparator)
        .ToLowerInvariant()
        .Replace("\u0001", $"{culture.DateTimeFormat.AMDesignator}/{culture.DateTimeFormat.PMDesignator}".ToLowerInvariant());

    public string Display(DateTime? value) => value?.ToString(display, culture) ?? "";

    public string Edit(DateTime? value) => edit is { } e ? value?.ToString(e.Edit, culture) ?? "" : Display(value);

    /// <summary>Typed or pasted text: the edit and display patterns, then anything the culture reads as a date and time.</summary>
    public bool TryParse(string text, out DateTime? value)
    {
        value = null;
        text = text.Trim();
        if (text.Length == 0)
            return true;
        var patterns = edit is { } e ? new[] { e.Edit, display } : [display];
        foreach (var pattern in patterns)
        {
            if (DateTime.TryParseExact(text, pattern, culture, Styles, out var exact))
            {
                value = exact;
                return true;
            }
        }
        if (DateTime.TryParse(text, culture, Styles, out var loose))
        {
            value = loose;
            return true;
        }
        return false;
    }
}
