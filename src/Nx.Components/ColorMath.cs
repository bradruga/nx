using System.Globalization;
using System.Text.RegularExpressions;

namespace Nx.Components;

/// <summary>A color as red, green and blue (0–255) and alpha (0–1).</summary>
internal readonly record struct Rgba(byte R, byte G, byte B, double A);

/// <summary>A color as hue (0–360), saturation and value (0–1) and alpha (0–1).</summary>
internal readonly record struct Hsva(double H, double S, double V, double A);

/// <summary>Parsing, formatting and HSV conversion of CSS colors, shared by <see cref="NxColorPicker"/> and <see cref="NxColorGradient"/>.</summary>
internal static partial class ColorMath
{
    [GeneratedRegex(@"^rgba?\(\s*(\d{1,3})[\s,]+(\d{1,3})[\s,]+(\d{1,3})(?:[\s,/]+([\d.]+)(%?))?", RegexOptions.IgnoreCase)]
    private static partial Regex Rgb();

    /// <summary>A hex (<c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>) or <c>rgb()</c>/<c>rgba()</c> color; <c>null</c> otherwise.</summary>
    public static Rgba? Parse(string? color)
    {
        var text = color?.Trim() ?? "";
        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            if (hex.Length is 3 or 4)
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            if (hex.Length is not (6 or 8) || !hex.All(Uri.IsHexDigit))
                return null;
            byte At(int i) => byte.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Rgba(At(0), At(2), At(4), hex.Length == 8 ? At(6) / 255.0 : 1);
        }
        var rgb = Rgb().Match(text);
        if (!rgb.Success)
            return null;
        byte Part(int i) => (byte)Math.Min(255, int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture));
        var alpha = 1.0;
        if (rgb.Groups[4].Success && double.TryParse(rgb.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
            alpha = Math.Clamp(rgb.Groups[5].Value == "%" ? a / 100 : a, 0, 1);
        return new Rgba(Part(1), Part(2), Part(3), alpha);
    }

    /// <summary><c>#rrggbb</c>, or <c>#rrggbbaa</c> when not opaque.</summary>
    public static string ToHex(Rgba c)
    {
        var hex = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        return c.A < 1 ? hex + ((byte)Math.Round(c.A * 255)).ToString("x2", CultureInfo.InvariantCulture) : hex;
    }

    /// <summary><c>rgb(r, g, b)</c>, or <c>rgba(r, g, b, a)</c> when not opaque.</summary>
    public static string ToRgb(Rgba c) => c.A < 1
        ? string.Create(CultureInfo.InvariantCulture, $"rgba({c.R}, {c.G}, {c.B}, {Math.Round(c.A, 2)})")
        : $"rgb({c.R}, {c.G}, {c.B})";

    public static Hsva ToHsv(Rgba c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        double h = 0;
        if (delta > 0)
        {
            h = max == r ? 60 * (((g - b) / delta + 6) % 6)
              : max == g ? 60 * ((b - r) / delta + 2)
              : 60 * ((r - g) / delta + 4);
        }
        return new Hsva(h, max == 0 ? 0 : delta / max, max, c.A);
    }

    public static Rgba ToRgba(Hsva c)
    {
        var chroma = c.V * c.S;
        var h = (c.H % 360 + 360) % 360 / 60;
        var x = chroma * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _   => (chroma, 0d, x)
        };
        var m = c.V - chroma;
        byte Byte(double v) => (byte)Math.Round(Math.Clamp(v + m, 0, 1) * 255);
        return new Rgba(Byte(r), Byte(g), Byte(b), c.A);
    }
}
