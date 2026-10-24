namespace Nx.Components;

/// <summary>Files an <see cref="NxFileSelect"/> is adding (<c>OnSelect</c>) or removing (<c>OnRemove</c>).</summary>
public sealed class NxFileSelectEventArgs
{
    internal NxFileSelectEventArgs(IReadOnlyList<NxSelectedFile> files) => Files = files;

    /// <summary>The files, valid or not (see <see cref="NxSelectedFile.IsValid"/>).</summary>
    public IReadOnlyList<NxSelectedFile> Files { get; }

    /// <summary>In <c>OnSelect</c>, set it to leave the files out of the list.</summary>
    public bool IsCancelled { get; set; }
}
