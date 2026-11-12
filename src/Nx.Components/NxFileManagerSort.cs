namespace Nx.Components;

/// <summary>What an <see cref="NxFileManager{TItem}"/> sorts a folder by; folders always come before files.</summary>
public enum NxFileManagerSort
{
    /// <summary>The name, as shown (with its extension).</summary>
    Name,

    /// <summary>The date modified.</summary>
    DateModified,

    /// <summary>The type (extension).</summary>
    Type,

    /// <summary>The size.</summary>
    Size
}
