namespace Nx.Components;

/// <summary>The kind of an <see cref="NxNotificationModel"/>.</summary>
public enum NxNotificationType
{
    /// <summary>Information, in the accent color. Default.</summary>
    Info,

    /// <summary>Something worked.</summary>
    Success,

    /// <summary>Something to look at.</summary>
    Warning,

    /// <summary>Something failed; announced at once (<c>role="alert"</c>).</summary>
    Error,

    /// <summary>A plain dark message (Telerik's "inverse"), e.g. "Saved by Dana at 9:12".</summary>
    Neutral
}
