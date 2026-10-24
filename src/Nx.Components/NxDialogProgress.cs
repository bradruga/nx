namespace Nx.Components;

/// <summary>
/// A "working…" dialog from <see cref="NxDialogService.CreateProgress"/>: nothing shows until the first
/// <see cref="Report"/>, each report replaces the text under the spinner, and <see cref="Close"/> or <c>Dispose</c>
/// ends it. Use it in a <c>using</c> block around long work.
/// </summary>
public sealed class NxDialogProgress : IProgress<string>, IDisposable
{
    private readonly Action<NxDialogProgress> onChanged;

    internal NxDialogProgress(string title, string id, Action<NxDialogProgress> onChanged)
    {
        Title = title;
        Id = id;
        this.onChanged = onChanged;
    }

    /// <summary>The dialog's title.</summary>
    public string Title { get; }

    /// <summary>The last reported text.</summary>
    public string? Text { get; private set; }

    /// <summary><c>true</c> from the first report until closed.</summary>
    public bool IsVisible => Text is not null && !IsClosed;

    /// <summary><c>true</c> once closed; later reports are ignored.</summary>
    public bool IsClosed { get; private set; }

    internal string Id { get; }

    /// <summary>Shows <paramref name="value"/> under the spinner, and the dialog if it was not showing yet. Safe from any thread.</summary>
    public void Report(string value)
    {
        if (IsClosed)
            return;
        Text = value;
        onChanged(this);
    }

    /// <summary>Closes the dialog for good.</summary>
    public void Close()
    {
        if (IsClosed)
            return;
        IsClosed = true;
        onChanged(this);
    }

    /// <summary>Closes the dialog.</summary>
    public void Dispose() => Close();
}
