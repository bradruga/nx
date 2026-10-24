using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Nx.Components;

/// <summary>Grid styles and label text shared by <see cref="NxForm"/>, <see cref="NxFormGroup"/> and <see cref="NxFormItem"/>.</summary>
internal static class FormLayout
{
    /// <summary>The inline style of an items grid: its column count and, when set, its column gap.</summary>
    public static string GridStyle(int columns, string? columnSpacing)
    {
        var style = $"--nx-form-columns: {Math.Max(1, columns).ToString(CultureInfo.InvariantCulture)}";
        return string.IsNullOrWhiteSpace(columnSpacing) ? style : $"{style}; --nx-form-column-gap: {columnSpacing}";
    }

    /// <summary>A property's label: its <c>[Display(Name)]</c> or <c>[DisplayName]</c>, else its name in words (<c>JobStartDate</c> → "Job start date").</summary>
    public static string LabelOf(PropertyInfo property)
    {
        if (property.GetCustomAttribute<DisplayAttribute>()?.GetName() is { Length: > 0 } display)
            return display;
        if (property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName is { Length: > 0 } displayName)
            return displayName;
        return Words(property.Name);
    }

    /// <summary><c>PourDateUtc</c> → "Pour date utc"; runs of capitals stay together (<c>PONumber</c> → "PO number").</summary>
    public static string Words(string name)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 1; i < name.Length; i++)
        {
            // A capital after a lower-case letter or digit starts a word, as does the last capital of an acronym before lower case.
            var boundary = char.IsUpper(name[i]) &&
                (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));
            if (boundary || name[i] == '_')
            {
                words.Add(name[start..i]);
                start = name[i] == '_' ? i + 1 : i;
            }
        }
        words.Add(name[start..]);

        var text = new StringBuilder(name.Length + words.Count);
        foreach (var word in words.Where(w => w.Length > 0))
        {
            var acronym = word.Length > 1 && word.All(char.IsUpper);
            text.Append(text.Length == 0 ? "" : " ").Append(text.Length == 0 || acronym ? word : word.ToLowerInvariant());
        }
        return text.ToString();
    }
}
