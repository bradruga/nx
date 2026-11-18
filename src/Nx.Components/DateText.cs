using System.Globalization;
using System.Text;

namespace Nx.Components;

/// <summary>Edit patterns, the time pickers' input masks and lenient parsing shared by the date and time pickers.</summary>
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

    /// <summary>
    /// Numbers typed in <paramref name="order"/> (pattern letters, e.g. <c>Mdy</c>), with or without separators or a
    /// year: <c>12/5</c>, <c>12/5/26</c>, <c>1205</c>, <c>12052026</c>. A missing year is this year.
    /// </summary>
    public static bool TryParseParts(string text, string order, CultureInfo culture, out DateOnly date)
    {
        date = default;
        if (order.Length != 3 || text.Any(char.IsLetter))
            return false;

        var tokens = Numbers(text);
        var noYear = string.Concat(order.Where(c => c != 'y'));
        if (tokens.Count == 1)
        {
            // No separators: two digits a part, the year two or four.
            var digits = tokens[0];
            var kinds = digits.Length == 4 ? noYear : order;
            if (digits.Length is not (4 or 6 or 8))
                return false;
            tokens.Clear();
            var at = 0;
            foreach (var kind in kinds)
            {
                var width = kind == 'y' && digits.Length == 8 ? 4 : 2;
                tokens.Add(digits.Substring(at, width));
                at += width;
            }
        }

        var parts = tokens.Count switch { 2 => noYear, 3 => order, _ => null };
        if (parts is null || tokens.Any(t => t.Length > 4) || (parts.Length == 3 && tokens[parts.IndexOf('y')].Length == 3))
            return false;

        int day = 0, month = 0, year = DateTime.Today.Year;
        for (var i = 0; i < parts.Length; i++)
        {
            var n = int.Parse(tokens[i], CultureInfo.InvariantCulture);
            if (parts[i] == 'd') day = n;
            else if (parts[i] == 'M') month = n;
            else year = tokens[i].Length <= 2 ? culture.Calendar.ToFourDigitYear(n) : n;
        }
        if (month is < 1 or > 12 || year is < 1 or > 9999 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;
        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>The runs of digits in <paramref name="text"/>, in order.</summary>
    private static List<string> Numbers(string text)
    {
        var numbers = new List<string>();
        var run = new StringBuilder();
        foreach (var c in text + " ")
        {
            if (char.IsAsciiDigit(c))
                run.Append(c);
            else if (run.Length > 0)
            {
                numbers.Add(run.ToString());
                run.Clear();
            }
        }
        return numbers;
    }

    /// <summary>Parses typed or pasted text: the given patterns, then numbers in <paramref name="order"/>, then anything the culture reads as a date.</summary>
    public static bool TryParseDate(string text, IEnumerable<string> patterns, string? order, CultureInfo culture, out DateOnly? date)
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
        if (order is not null)
        {
            if (TryParseParts(text, order, culture, out var typed))
            {
                date = typed;
                return true;
            }
            // Two bare numbers are a month and a day, never the culture's month and year ("2/30" as February 1930).
            if (!text.Any(char.IsLetter) && Numbers(text).Count == 2)
                return false;
        }
        if (DateTime.TryParse(text, culture, Styles, out var loose))
        {
            date = DateOnly.FromDateTime(loose);
            return true;
        }
        return false;
    }
}

/// <summary>A date picker's display and edit formats for one Format and culture: formats and parses.</summary>
internal sealed class DateFormatter
{
    private readonly CultureInfo culture;
    private readonly string display;
    private readonly (string Edit, string Mask)? edit;

    public DateFormatter(string format, CultureInfo culture)
    {
        this.culture = culture;
        display = DateText.Expand(format, culture);
        // The all-number form of the format, else of the culture's short date.
        edit = DateText.Fixed(display, culture) ?? DateText.Fixed(culture.DateTimeFormat.ShortDatePattern, culture);
    }

    /// <summary>The edit text's shape, e.g. <c>99/99/9999</c>, for stepping its parts; <c>null</c> for none.</summary>
    public string? Shape => edit?.Mask;

    /// <summary>The order of the date parts as pattern letters, e.g. <c>Mdy</c>; <c>null</c> without an all-number edit pattern.</summary>
    public string? Order => edit is { } e ? string.Concat(e.Edit.Where(c => c is 'd' or 'M' or 'y').Distinct()) : null;

    /// <summary>The edit pattern as a hint, e.g. <c>mm/dd/yyyy</c>.</summary>
    public string? Hint => edit?.Edit.Replace("/", culture.DateTimeFormat.DateSeparator).ToLowerInvariant();

    public string Display(DateOnly? date) => date?.ToString(display, culture) ?? "";

    public string Edit(DateOnly? date) => edit is { } e ? date?.ToString(e.Edit, culture) ?? "" : Display(date);

    public bool TryParse(string text, out DateOnly? date) =>
        DateText.TryParseDate(text, edit is { } e ? [e.Edit, display] : [display], Order, culture, out date);
}
