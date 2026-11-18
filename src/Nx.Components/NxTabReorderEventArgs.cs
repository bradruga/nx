namespace Nx.Components;

/// <summary>Raised by <see cref="NxTabStrip"/> when the user moves a tab. The strip already shows the new order; save it, or reorder your list to match.</summary>
public sealed class NxTabReorderEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NxTabReorderEventArgs(string tabId, int oldIndex, int newIndex)
    {
        TabId = tabId;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The moved tab's <see cref="NxTab.Id"/> (or title, when it has no Id).</summary>
    public string TabId { get; }

    /// <summary>Its place before, from <c>0</c>.</summary>
    public int OldIndex { get; }

    /// <summary>Its place now, from <c>0</c>.</summary>
    public int NewIndex { get; }
}
