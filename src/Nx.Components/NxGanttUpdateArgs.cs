namespace Nx.Components;

/// <summary>
/// Passed to <see cref="NxGantt{TItem}.OnUpdate"/> after any edit, as NxGrid's update args. A dragged bar has not
/// touched the item: write <see cref="Start"/> and <see cref="End"/> (or your own dates) and save. A cell's editor has already written it.
/// </summary>
/// <typeparam name="TItem">The task type.</typeparam>
/// <example>
/// <code>
/// async Task SaveAsync(NxGanttUpdateArgs&lt;Job&gt; args)
/// {
///     if (args.Start is { } start) args.Item.Start = start;
///     if (args.End is { } end) args.Item.End = end;
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

    /// <summary>The start a dragged bar asks for; <c>null</c> when it stays.</summary>
    public DateTime? Start { get; init; }

    /// <summary>The end a dragged bar asks for; <c>null</c> when it stays.</summary>
    public DateTime? End { get; init; }

    /// <summary>For a cell: its column's value before the edit.</summary>
    public object? OldValue { get; init; }

    /// <summary>For a cell: its column's value now; equal to <see cref="OldValue"/> when nothing changed.</summary>
    public object? NewValue { get; init; }
}
