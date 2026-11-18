namespace Nx.Components;

/// <summary>
/// Passed to <see cref="NxGantt{TItem}.OnUpdate"/> after any edit, as NxGrid's update args. A dragged bar has not
/// touched the item: apply the <see cref="Changes"/> (or your own dates) and save. A cell's editor has already written it.
/// </summary>
/// <typeparam name="TItem">The task type.</typeparam>
/// <example>
/// <code>
/// async Task SaveAsync(NxGanttUpdateArgs&lt;Job&gt; args)
/// {
///     args.Apply();
///     await db.SaveAsync(args.Item);
/// }
/// </code>
/// </example>
public sealed class NxGanttUpdateArgs<TItem>
{
    /// <summary>The task.</summary>
    public required TItem Item { get; init; }

    /// <summary>What the user did.</summary>
    public required NxGanttUpdateKind Kind { get; init; }

    /// <summary>The properties that change: the start and end for a drag, the column's field for a cell.</summary>
    public required IReadOnlyList<NxGanttChange<TItem>> Changes { get; init; }

    /// <summary>Writes every change to <see cref="Item"/>.</summary>
    public void Apply()
    {
        foreach (var change in Changes)
            change.Apply(Item);
    }
}
