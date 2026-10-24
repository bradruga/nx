namespace Nx.Components;

public partial class NxComboBox<TItem, TValue> : IComboBoxColumnHost<TItem>
{
    private const string DefaultColumnWidth = "minmax(100px, 1fr)";
    private const string CheckColumnWidth = "22px";

    private readonly List<NxComboBoxColumnDefinition<TItem>> declaredColumns = [];
    private bool disposed;

    private IReadOnlyList<NxComboBoxColumnDefinition<TItem>> EffectiveColumns =>
        ColumnDefinitions as IReadOnlyList<NxComboBoxColumnDefinition<TItem>>
        ?? ColumnDefinitions?.ToList()
        ?? (IReadOnlyList<NxComboBoxColumnDefinition<TItem>>)declaredColumns;

    private bool HasColumns => EffectiveColumns.Count > 0;

    /// <summary>The grid template shared by the header and every row: one track per column, then the check mark.</summary>
    private string GridStyle =>
        $"grid-template-columns: {string.Join(' ', EffectiveColumns.Select(c => c.Width ?? DefaultColumnWidth))} {CheckColumnWidth}";

    private static string AlignClass(NxColumnAlignment alignment) => alignment switch
    {
        NxColumnAlignment.Center => "nx-combobox-cell nx-combobox-cell-center",
        NxColumnAlignment.End    => "nx-combobox-cell nx-combobox-cell-end",
        _                        => "nx-combobox-cell"
    };

    void IComboBoxColumnHost<TItem>.SetColumn(NxComboBoxColumnDefinition<TItem>? previous, NxComboBoxColumnDefinition<TItem> column)
    {
        var index = previous is null ? -1 : declaredColumns.IndexOf(previous);
        if (index >= 0)
        {
            // A re-rendered column replaces itself in place. No re-render: the new definition is used
            // from the next render on, and re-rendering here would re-render the column, and so on.
            declaredColumns[index] = column;
            return;
        }
        declaredColumns.Add(column);
        StateHasChanged();
    }

    void IComboBoxColumnHost<TItem>.RemoveColumn(NxComboBoxColumnDefinition<TItem> column)
    {
        if (declaredColumns.Remove(column) && !disposed)
            StateHasChanged();
    }
}
