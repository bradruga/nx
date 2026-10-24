using System.Globalization;
using System.Text;

namespace Nx.Components;

/// <summary>Edit patterns, input masks and lenient parsing shared by the date and time pickers.</summary>
internal static class DateText
{
    private const DateTimeStyles Styles = DateTimeStyles.AllowWhiteSpaces;

    /// <summary>A one-letter standard format (<c>"d"</c>, <c>"t"</c>) as the culture's custom pattern; others unchanged.</summary>
    public static string Expand(string format, CultureInfo culture) =>
        format.Length == 1 ? culture.DateTimeFormat.GetAllDateTimePatterns(format[0])[0] : format;

    /// <summary>
    /// The fixed-width form of an all-number pattern and its mask: <c>"M/d/yyyy"</c> gives
    /// <c>("MM/dd/yyyy", "99/99/9999")</c>. <c>null</c> when the pattern has names, quotes, AM/PM or other parts.
    /// </summary>
    public static (string Edit, string Mask)? Fixed(string pattern, CultureInfo culture)
    {
        var edit = new StringBuilder();
        var mask = new StringBuilder();
        for (var i = 0; i < pattern.Length;)
        {
            var c = pattern[i];
            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c)
                run++;
            i += run;

            (string Token, int Width)? field = c switch
            {
                'd' when run <= 2 => ("dd", 2),
                'M' when run <= 2 => ("MM", 2),
                'y'               => ("yyyy", 4),
                'H' when run <= 2 => ("HH", 2),
                'm' when run <= 2 => ("mm", 2),
                _                 => null
            };
            if (field is { } f)
            {
                edit.Append(f.Token);
                mask.Append('9', f.Width);
            }
            else if (char.IsLetter(c) || c is '\'' or '"' or '\\' or '%' || char.IsDigit(c))
                return null;
            else
            {
                // In a .NET pattern ':' and '/' stand for the culture's separators.
                var literal = c switch
                {
                    '/' => culture.DateTimeFormat.DateSeparator,
                    ':' => culture.DateTimeFormat.TimeSeparator,
                    _   => c.ToString()
                };
                edit.Append(c, run);
                for (var r = 0; r < run; r++)
                    mask.Append(literal);
            }
        }
        return mask.ToString().Contains('9') ? (edit.ToString(), mask.ToString()) : null;
    }

    /// <summary>Parses typed or pasted text: the given patterns first, then anything the culture reads as a date.</summary>
    public static bool TryParseDate(string text, IEnumerable<string> patterns, CultureInfo culture, out DateOnly? date)
    {
        date = null;
        text = text.Trim();
        if (text.Length == 0)
            return true;
        foreach (var pattern in patterns.Append("yyyy-MM-dd"))
        {
            if (DateTime.TryParseExact(text, pattern, culture, Styles, out var exact))
            {
                date = DateOnly.FromDateTime(exact);
                return true;
            }
        }
        if (DateTime.TryParse(text, culture, Styles, out var loose))
        {
            date = DateOnly.FromDateTime(loose);
            return true;
        }
        return false;
    }
}

/// <summary>A date picker's display and edit formats for one Format and culture: formats, parses, masks.</summary>
internal sealed class DateFormatter
{
    private readonly CultureInfo culture;
    private readonly string display;
    private readonly (string Edit, string Mask)? edit;

    public DateFormatter(string format, CultureInfo culture)
    {
        this.culture = culture;
        display = DateText.Expand(format, culture);
        // The all-number form of the format, else of the culture's short date; neither leaves typing unmasked.
        edit = DateText.Fixed(display, culture) ?? DateText.Fixed(culture.DateTimeFormat.ShortDatePattern, culture);
    }

    /// <summary>The input mask, e.g. <c>99/99/9999</c>; <c>null</c> for none.</summary>
    public string? Mask => edit?.Mask;

    /// <summary>The edit pattern as a hint, e.g. <c>mm/dd/yyyy</c>.</summary>
    public string? Hint => edit?.Edit.Replace("/", culture.DateTimeFormat.DateSeparator).ToLowerInvariant();

    public string Display(DateOnly? date) => date?.ToString(display, culture) ?? "";

    public string Edit(DateOnly? date) => edit is { } e ? date?.ToString(e.Edit, culture) ?? "" : Display(date);

    public bool TryParse(string text, out DateOnly? date) =>
        DateText.TryParseDate(text, edit is { } e ? [e.Edit, display] : [display], culture, out date);
}
