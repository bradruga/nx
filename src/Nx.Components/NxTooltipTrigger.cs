namespace Nx.Components;

/// <summary>What shows an <see cref="NxTooltip"/>. Combine with <c>|</c>. A long press always shows it on touch screens.</summary>
[Flags]
public enum NxTooltipTrigger
{
    /// <summary>Only <c>ShowAsync</c> shows it.</summary>
    None = 0,

    /// <summary>The pointer resting on the anchor.</summary>
    Hover = 1,

    /// <summary>Keyboard focus on the anchor.</summary>
    Focus = 2,

    /// <summary>A click on the anchor toggles it.</summary>
    Click = 4
}
