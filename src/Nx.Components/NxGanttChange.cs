namespace Nx.Components;

/// <summary>One property an <see cref="NxGantt{TItem}"/> update changes, as NxGrid's cell change: the old and new value, and <see cref="Apply"/> to write it.</summary>
/// <typeparam name="TItem">The task type.</typeparam>
public sealed class NxGanttChange<TItem>
{
    /// <summary>The property's name, e.g. the Gantt's <c>StartField</c>.</summary>
    public required string Field { get; init; }

    /// <summary>The value before.</summary>
    public object? OldValue { get; init; }

    /// <summary>The value asked for, in the property's own type.</summary>
    public object? NewValue { get; init; }

    internal Action<TItem>? ApplyAction { get; init; }

    /// <summary>Writes <see cref="NewValue"/> to the item.</summary>
    public void Apply(TItem item) => ApplyAction?.Invoke(item);
}
