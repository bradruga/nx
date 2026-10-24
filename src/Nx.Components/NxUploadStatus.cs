namespace Nx.Components;

/// <summary>Where an <see cref="NxUploadFile"/> is in its upload.</summary>
public enum NxUploadStatus
{
    /// <summary>Not accepted (type or size); never sent.</summary>
    Invalid,

    /// <summary>Accepted and waiting to be sent (in the queue, or for <c>UploadFiles()</c> without AutoUpload).</summary>
    Waiting,

    /// <summary>Being sent.</summary>
    Uploading,

    /// <summary>The server answered with a 2xx status.</summary>
    Uploaded,

    /// <summary>The server answered with another status, or could not be reached.</summary>
    Failed,

    /// <summary>The user cancelled it.</summary>
    Cancelled
}
