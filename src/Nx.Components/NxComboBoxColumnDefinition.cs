using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// One column of a multi-column <c>NxComboBox</c> list. Pass a list of these as <c>ColumnDefinitions</c>,
/// or declare <see cref="NxComboBoxColumn{TItem}"/> children inside <c>Columns</c>.
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
public sealed class NxComboBoxColumnDefinition<TItem>
{
    /// <summary>The cell value. Shown with <see cref="Format"/> and matched by filtering.</summary>
    public Func<TItem, object?>? Field { get; init; }

    /// <summary>Header text.</summary>
    public string? Header { get; init; }

    /// <summary>A CSS grid track size: <c>"80px"</c>, <c>"2fr"</c>, <c>"minmax(100px, 1fr)"</c>. Default: <c>minmax(100px, 1fr)</c>.</summary>
    public string? Width { get; init; }

    /// <summary>Header and cell alignment. Default: <see cref="NxColumnAlignment.Start"/>.</summary>
    public NxColumnAlignment Alignment { get; init; }

    /// <summary>Format string for an <see cref="IFormattable"/> field value, e.g. <c>"N2"</c> or <c>"d"</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Renders the cell instead of the formatted field value.</summary>
    public RenderFragment<TItem>? Template { get; init; }

    internal string TextOf(TItem item) => Field?.Invoke(item) switch
    {
        null => "",
        IFormattable f when Format is not null => f.ToString(Format, null),
        var v => v.ToString() ?? ""
    };
}
