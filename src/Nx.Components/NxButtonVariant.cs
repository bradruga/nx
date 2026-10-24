namespace Nx.Components;

/// <summary>Visual style of an <see cref="NxButton"/>. Set via <see cref="NxButton.Variant"/>.</summary>
public enum NxButtonVariant
{
    /// <summary>Filled with the accent color. The main action on a form or dialog.</summary>
    Primary,

    /// <summary>Bordered, neutral surface. Default. Every other action.</summary>
    Secondary,

    /// <summary>Filled with the danger color. Destructive actions such as Delete.</summary>
    Danger,

    /// <summary>No border or fill until hovered. Toolbars and dense rows.</summary>
    Ghost,

    /// <summary>Looks like a hyperlink; still a button. Inline actions in text.</summary>
    Link
}
