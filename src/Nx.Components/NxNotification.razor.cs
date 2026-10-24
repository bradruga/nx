using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// Short messages ("toasts") stacked in a corner of the screen: place one, keep a <c>@ref</c>, and call
/// <see cref="Show(NxNotificationModel)"/>. Each closes after its <c>CloseAfter</c> (paused while the pointer or focus
/// is on it) or by its close button. Errors are announced at once, the rest politely. Full width on a phone.
/// </summary>
/// <example>
/// <code>
/// &lt;NxNotification @@ref="notification" /&gt;
///
/// notification.Show(new NxNotificationModel { Text = "Saved", Type = NxNotificationType.Success });
/// </code>
/// </example>
public partial class NxNotification : IDisposable
{
    private const int LeaveMs = 180;    // the exit animation, in nx-notification.razor.css

    private readonly List<Entry> entries = [];
    private bool disposed;

    /// <summary>One shown message and its timer.</summary>
    private sealed class Entry(NxNotificationModel model)
    {
        public NxNotificationModel Model { get; } = model;
        public CancellationTokenSource? Timer { get; set; }
        public bool Leaving { get; set; }
    }

    /// <summary>Left, center or right. Default: <see cref="NxNotificationHorizontalPosition.Right"/>.</summary>
    [Parameter] public NxNotificationHorizontalPosition HorizontalPosition { get; set; } = NxNotificationHorizontalPosition.Right;

    /// <summary>Top or bottom. Default: <see cref="NxNotificationVerticalPosition.Bottom"/>.</summary>
    [Parameter] public NxNotificationVerticalPosition VerticalPosition { get; set; } = NxNotificationVerticalPosition.Bottom;

    /// <summary>The most messages shown at once; a new one beyond it closes the oldest. Default: <c>5</c>.</summary>
    [Parameter] public int MaxVisible { get; set; } = 5;

    /// <summary>Extra CSS class on the stack, e.g. to move it with <c>--nx-notification-offset</c>.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the stack.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the stack element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The messages showing now, oldest first.</summary>
    public IReadOnlyList<NxNotificationModel> Visible => entries.Where(e => !e.Leaving).Select(e => e.Model).ToList();

    /// <summary>Shows a message. Safe to call from any thread. Returns the model, for <see cref="Hide"/>.</summary>
    public NxNotificationModel Show(NxNotificationModel model)
    {
        _ = InvokeAsync(() =>
        {
            if (disposed)
                return;
            var entry = new Entry(model);
            entries.Add(entry);
            foreach (var old in entries.Where(e => !e.Leaving).SkipLast(Math.Max(1, MaxVisible)).ToList())
                _ = LeaveAsync(old);
            StartTimer(entry);
            StateHasChanged();
        });
        return model;
    }

    /// <summary>Shows <paramref name="text"/> as a message of <paramref name="type"/>, with the defaults.</summary>
    public NxNotificationModel Show(string text, NxNotificationType type = NxNotificationType.Info) =>
        Show(new NxNotificationModel { Text = text, Type = type });

    /// <summary>Closes a message.</summary>
    public void Hide(NxNotificationModel model) => _ = InvokeAsync(async () =>
    {
        if (entries.FirstOrDefault(e => ReferenceEquals(e.Model, model) && !e.Leaving) is { } entry)
            await LeaveAsync(entry);
    });

    /// <summary>Closes every message.</summary>
    public void HideAll() => _ = InvokeAsync(async () =>
        await Task.WhenAll(entries.Where(e => !e.Leaving).ToList().Select(LeaveAsync)));

    private void StartTimer(Entry entry)
    {
        entry.Timer?.Cancel();
        entry.Timer = null;
        if (entry.Model.CloseAfter <= 0 || entry.Leaving)
            return;
        var cts = entry.Timer = new CancellationTokenSource();
        _ = CloseLaterAsync(entry, entry.Model.CloseAfter, cts.Token);
    }

    private async Task CloseLaterAsync(Entry entry, int delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await InvokeAsync(() => LeaveAsync(entry));
    }

    // The pointer or focus on a message holds it open; leaving counts its CloseAfter again from the start.
    private void Pause(Entry entry)
    {
        entry.Timer?.Cancel();
        entry.Timer = null;
    }

    private void Resume(Entry entry) => StartTimer(entry);

    private async Task LeaveAsync(Entry entry)
    {
        if (entry.Leaving || disposed)
            return;
        entry.Leaving = true;
        entry.Timer?.Cancel();
        StateHasChanged();
        await Task.Delay(LeaveMs);
        if (disposed)
            return;
        entries.Remove(entry);
        StateHasChanged();
    }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-notification-stack",
        VerticalPosition == NxNotificationVerticalPosition.Top ? "nx-notification-top" : "nx-notification-bottom",
        HorizontalPosition switch
        {
            NxNotificationHorizontalPosition.Left   => "nx-notification-left",
            NxNotificationHorizontalPosition.Center => "nx-notification-center",
            _                                       => "nx-notification-right"
        },
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private static string ItemClass(Entry entry) => string.Join(' ', new[]
    {
        "nx-notification",
        entry.Model.Type switch
        {
            NxNotificationType.Success => "nx-notification-success",
            NxNotificationType.Warning => "nx-notification-warning",
            NxNotificationType.Error   => "nx-notification-error",
            NxNotificationType.Neutral => "nx-notification-neutral",
            _                          => "nx-notification-info"
        },
        entry.Leaving ? "nx-notification-leaving" : null
    }.Where(c => c is not null));

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
        foreach (var entry in entries)
            entry.Timer?.Cancel();
    }
}
