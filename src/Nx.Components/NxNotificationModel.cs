namespace Nx.Components;

/// <summary>One message shown by <see cref="NxNotification.Show(NxNotificationModel)"/>.</summary>
public sealed class NxNotificationModel
{
    /// <summary>The message.</summary>
    public string Text { get; set; } = "";

    /// <summary>The kind of message: its color, icon and how urgently it is announced. Default: <see cref="NxNotificationType.Info"/>.</summary>
    public NxNotificationType Type { get; set; }

    /// <summary>Shows a close button. Default: <c>true</c>.</summary>
    public bool Closable { get; set; } = true;

    /// <summary>Milliseconds before it closes by itself, counted again after the pointer or focus leaves it; <c>0</c> keeps it until closed. Default: <c>5000</c>.</summary>
    public int CloseAfter { get; set; } = 5000;

    /// <summary>Shows the type's icon. Default: <c>true</c>.</summary>
    public bool ShowIcon { get; set; } = true;
}
