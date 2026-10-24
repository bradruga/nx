namespace Nx.Components;

/// <summary>A file in an <see cref="NxUpload"/>'s list and how its upload went.</summary>
public sealed class NxUploadFile
{
    internal NxUploadFile(string id, string name, long size, NxFileError? error)
    {
        Id = id;
        Name = name;
        Size = size;
        Error = error;
        Extension = FileRules.ExtensionOf(name);
        Status = error is null ? NxUploadStatus.Waiting : NxUploadStatus.Invalid;
    }

    /// <summary>A unique id for this pick.</summary>
    public string Id { get; }

    /// <summary>The file name, without a path.</summary>
    public string Name { get; }

    /// <summary>The lower-case extension with its dot, e.g. <c>.pdf</c>.</summary>
    public string Extension { get; }

    /// <summary>The size in bytes.</summary>
    public long Size { get; }

    /// <summary>Why the file is not accepted; <c>null</c> when it is.</summary>
    public NxFileError? Error { get; }

    /// <summary>Where the file is in its upload.</summary>
    public NxUploadStatus Status { get; internal set; }

    /// <summary>Percent sent, 0 to 100.</summary>
    public int Progress { get; internal set; }

    /// <summary>The server's HTTP status; 0 when the request did not reach it.</summary>
    public int StatusCode { get; internal set; }

    /// <summary>The server's response body, e.g. the id it stored the file under.</summary>
    public string? ResponseText { get; internal set; }
}
