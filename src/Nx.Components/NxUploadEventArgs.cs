namespace Nx.Components;

/// <summary>A file an <see cref="NxUpload"/> event is about: uploaded, failed, cancelled or removed.</summary>
public sealed class NxUploadEventArgs
{
    internal NxUploadEventArgs(NxUploadFile file) => File = file;

    /// <summary>The file; <see cref="NxUploadFile.ResponseText"/> and <see cref="NxUploadFile.StatusCode"/> hold the server's answer.</summary>
    public NxUploadFile File { get; }
}
