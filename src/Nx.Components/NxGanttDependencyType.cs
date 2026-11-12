namespace Nx.Components;

/// <summary>How a dependency links two tasks in an <see cref="NxGantt{TItem}"/>; the values are Telerik's, in its order.</summary>
public enum NxGanttDependencyType
{
    /// <summary>The successor finishes when the predecessor finishes.</summary>
    FinishFinish,

    /// <summary>The successor starts when the predecessor finishes (the usual one).</summary>
    FinishStart,

    /// <summary>The successor finishes when the predecessor starts.</summary>
    StartFinish,

    /// <summary>The successor starts when the predecessor starts.</summary>
    StartStart
}
