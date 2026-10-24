using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>A file picked in an <see cref="NxFileSelect"/>; read it with <see cref="OpenReadStream"/>.</summary>
public sealed class NxSelectedFile
{
    internal NxSelectedFile(IBrowserFile file, NxFileError? error)
    {
        BrowserFile = file;
        Error = error;
        Extension = FileRules.ExtensionOf(file.Name);
    }

    /// <summary>A unique id for this pick.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The file name, without a path.</summary>
    public string Name => BrowserFile.Name;

    /// <summary>The lower-case extension with its dot, e.g. <c>.pdf</c>.</summary>
    public string Extension { get; }

    /// <summary>The size in bytes.</summary>
    public long Size => BrowserFile.Size;

    /// <summary>Why the file is not accepted; <c>null</c> when it is.</summary>
    public NxFileError? Error { get; }

    /// <summary>The file passes the type and size limits.</summary>
    public bool IsValid => Error is null;

    /// <summary>The file as Blazor gives it: content type, last modified, and the stream.</summary>
    public IBrowserFile BrowserFile { get; }

    /// <summary>
    /// The file's content, streamed from the browser. <paramref name="maxAllowedSize"/> defaults to the file's own size;
    /// read it while the component is on the page.
    /// </summary>
    public Stream OpenReadStream(long? maxAllowedSize = null, CancellationToken cancellationToken = default) =>
        BrowserFile.OpenReadStream(maxAllowedSize ?? Math.Max(Size, 1), cancellationToken);
}
