namespace Nx.Components;

/// <summary>Files an <see cref="NxUpload"/> is about to list and send (<c>OnSelect</c>).</summary>
public sealed class NxUploadSelectEventArgs
{
    internal NxUploadSelectEventArgs(IReadOnlyList<NxUploadFile> files) => Files = files;

    /// <summary>The files, valid or not (see <see cref="NxUploadFile.Error"/>).</summary>
    public IReadOnlyList<NxUploadFile> Files { get; }

    /// <summary>Set it to leave the files out: not listed, not sent.</summary>
    public bool IsCancelled { get; set; }
}
