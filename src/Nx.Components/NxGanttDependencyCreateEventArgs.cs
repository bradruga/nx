namespace Nx.Components;

/// <summary>A dependency the user drew from one task's bar to another's; add it to the dependencies to keep it.</summary>
/// <param name="Predecessor">The task the arrow starts from.</param>
/// <param name="Successor">The task it points to.</param>
/// <param name="Type">From the predecessor's start or end to the successor's start or end.</param>
public sealed record NxGanttDependencyCreateEventArgs<TItem>(TItem Predecessor, TItem Successor, NxGanttDependencyType Type);
