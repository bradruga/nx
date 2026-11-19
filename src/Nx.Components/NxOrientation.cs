namespace Nx.Components;

/// <summary>How the items of an <see cref="NxRadioGroup{TItem, TValue}"/>, or the panes of an <see cref="NxSplitter"/>, are laid out.</summary>
public enum NxOrientation
{
    /// <summary>One under another. The radio group's default.</summary>
    Vertical,

    /// <summary>Side by side (radio items wrap when they do not fit). The splitter's default.</summary>
    Horizontal
}
