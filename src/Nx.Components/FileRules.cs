using System.Globalization;

namespace Nx.Components;

/// <summary>The extension and size checks, hint and size text shared by <see cref="NxFileSelect"/> and <see cref="NxUpload"/>.</summary>
internal static class FileRules
{
    /// <summary>The lower-case extension with its dot, e.g. <c>.pdf</c>; empty for none.</summary>
    public static string ExtensionOf(string name) => Path.GetExtension(name).ToLowerInvariant();

    /// <summary>Extensions as <c>.pdf</c>, whether given as <c>pdf</c>, <c>.PDF</c> or <c>*.pdf</c>.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? extensions) =>
        extensions?.Select(e => e.Trim().TrimStart('*').ToLowerInvariant())
                   .Where(e => e.Length > 0)
                   .Select(e => e.StartsWith('.') ? e : "." + e)
                   .Distinct()
                   .ToList() ?? [];

    public static NxFileError? Check(string name, long size, IReadOnlyList<string> extensions, long? minSize, long? maxSize)
    {
        if (extensions.Count > 0 && !extensions.Contains(ExtensionOf(name)))
            return NxFileError.InvalidExtension;
        if (maxSize is long max && size > max)
            return NxFileError.TooLarge;
        if (minSize is long min && size < min)
            return NxFileError.TooSmall;
        return null;
    }

    public static string Message(NxFileError error, long? minSize, long? maxSize) => error switch
    {
        NxFileError.InvalidExtension => "This file type is not allowed.",
        NxFileError.TooLarge         => $"Larger than the {Size(maxSize ?? 0)} limit.",
        _                            => $"Smaller than the {Size(minSize ?? 0)} minimum."
    };

    /// <summary>"PDF, PNG · up to 10 MB" for the drop zone; null when there are no limits.</summary>
    public static string? Hint(IReadOnlyList<string> extensions, long? maxSize)
    {
        var parts = new List<string>();
        if (extensions.Count > 0)
            parts.Add(string.Join(", ", extensions.Select(e => e.TrimStart('.').ToUpperInvariant())));
        if (maxSize is long max)
            parts.Add($"up to {Size(max)}");
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>The <c>accept</c> attribute for the extensions, or null for any file.</summary>
    public static string? Accept(IReadOnlyList<string> extensions) => extensions.Count == 0 ? null : string.Join(",", extensions);

    /// <summary>A byte count for people: 512 B, 14 KB, 2.4 MB, 1.1 GB.</summary>
    public static string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = unit == 0 || value >= 10 ? "0" : "0.#";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {units[unit]}";
    }
}
