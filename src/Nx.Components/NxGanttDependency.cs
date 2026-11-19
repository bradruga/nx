namespace Nx.Components;

/// <summary>A link between two tasks of an <see cref="NxGantt{TItem}"/>, drawn as an arrow.</summary>
/// <param name="PredecessorId">The id of the task the arrow starts from.</param>
/// <param name="SuccessorId">The id of the task it points to.</param>
/// <param name="Type">From the predecessor's start or end to the successor's start or end.</param>
public sealed record NxGanttDependency(object PredecessorId, object SuccessorId, NxGanttDependencyType Type = NxGanttDependencyType.FinishStart);
