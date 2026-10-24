namespace Nx.Components;

/// <summary>What an <see cref="NxToolBar"/> does with buttons that do not fit on one line.</summary>
public enum NxToolBarOverflow
{
    /// <summary>They move into a More menu at the end.</summary>
    Menu,

    /// <summary>The bar wraps onto more lines.</summary>
    Wrap,

    /// <summary>The bar scrolls sideways.</summary>
    Scroll
}
