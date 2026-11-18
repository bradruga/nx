namespace Nx.Components;

/// <summary>How an <see cref="NxFileManager{TItem}"/> lists a folder's contents.</summary>
public enum NxFileManagerView
{
    /// <summary>Rows with name, date modified, type and size.</summary>
    Grid,

    /// <summary>Large icons with the name under each.</summary>
    Tiles
}
