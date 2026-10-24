namespace Nx.Components;

/// <summary>Why a file picked in an <see cref="NxFileSelect"/> or <see cref="NxUpload"/> was not accepted.</summary>
public enum NxFileError
{
    /// <summary>Its extension is not one of <c>AllowedExtensions</c>.</summary>
    InvalidExtension,

    /// <summary>It is larger than <c>MaxFileSize</c>.</summary>
    TooLarge,

    /// <summary>It is smaller than <c>MinFileSize</c>.</summary>
    TooSmall
}
