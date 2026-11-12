namespace Nx.Components;

/// <summary>How an <see cref="NxPdfViewer"/> sizes the pages when a document opens.</summary>
public enum NxPdfViewerZoom
{
    /// <summary>The page's width, up to its actual size.</summary>
    Auto,

    /// <summary>The page's width fills the viewer.</summary>
    PageWidth,

    /// <summary>A whole page fits in the viewer.</summary>
    PageFit,

    /// <summary>100%.</summary>
    ActualSize
}
