namespace Nx.Components;

/// <summary>Lets <see cref="NxComboBoxColumn{TItem}"/> children register with the combo box that holds them.</summary>
internal interface IComboBoxColumnHost<TItem>
{
    /// <summary>Adds <paramref name="column"/>, or puts it in the place of <paramref name="previous"/>.</summary>
    void SetColumn(NxComboBoxColumnDefinition<TItem>? previous, NxComboBoxColumnDefinition<TItem> column);

    void RemoveColumn(NxComboBoxColumnDefinition<TItem> column);
}
