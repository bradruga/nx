namespace Nx.Components;

/// <summary>The groups of tools on an <see cref="NxPdfViewer"/>'s toolbar; combine with <c>|</c>.</summary>
[Flags]
public enum NxPdfViewerTools
{
    /// <summary>No toolbar.</summary>
    None = 0,

    /// <summary>Previous / next page and the page number.</summary>
    Pager = 1,

    /// <summary>Zoom out / in and the zoom list.</summary>
    Zoom = 2,

    /// <summary>Find text, with previous / next match.</summary>
    Search = 4,

    /// <summary>Save the file.</summary>
    Download = 8,

    /// <summary>Print through the browser.</summary>
    Print = 16,

    /// <summary>Every tool.</summary>
    All = Pager | Zoom | Search | Download | Print
}
