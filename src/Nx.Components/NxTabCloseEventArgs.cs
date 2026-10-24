namespace Nx.Components;

/// <summary>Raised by <see cref="NxTabStrip"/> when the user closes a closable tab. Remove the tab from your list.</summary>
public sealed class NxTabCloseEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NxTabCloseEventArgs(string tabId) => TabId = tabId;

    /// <summary>The closed tab's <see cref="NxTab.Id"/> (or title, when it has no Id).</summary>
    public string TabId { get; }
}
