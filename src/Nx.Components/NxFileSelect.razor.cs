using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// Picks files to read in .NET — no upload endpoint: a drop zone (click or drop), a list with type and size checks and
/// remove buttons, and <c>OnSelect</c> with each file's stream (<see cref="NxSelectedFile.OpenReadStream"/>). Built on
/// Blazor's <c>InputFile</c>. To send files to a URL with progress, use <see cref="NxUpload"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxFileSelect AllowedExtensions="new[] { ".pdf", ".png" }" MaxFileSize="10_000_000" OnSelect="AddAsync" /&gt;
/// </code>
/// </example>
public partial class NxFileSelect
{
    private readonly string generatedId = $"nx-fileselect-{Guid.NewGuid():N}"[..22];
    private readonly List<NxSelectedFile> files = [];
    private IReadOnlyList<string> extensions = [];
    private int pickCount;

    /// <summary>Lets several files be picked at once, and keeps adding to the list. Default: <c>true</c>.</summary>
    [Parameter] public bool Multiple { get; set; } = true;

    /// <summary>The extensions accepted, e.g. <c>.pdf</c>, <c>.png</c>; others are listed as not allowed. Default: any.</summary>
    [Parameter] public IEnumerable<string>? AllowedExtensions { get; set; }

    /// <summary>The largest file accepted, in bytes. Default: no limit.</summary>
    [Parameter] public long? MaxFileSize { get; set; }

    /// <summary>The smallest file accepted, in bytes. Default: no limit.</summary>
    [Parameter] public long? MinFileSize { get; set; }

    /// <summary>Raised with the files just picked, before they are listed; set <c>IsCancelled</c> to leave them out.</summary>
    [Parameter] public EventCallback<NxFileSelectEventArgs> OnSelect { get; set; }

    /// <summary>Raised with a file the user removed from the list.</summary>
    [Parameter] public EventCallback<NxFileSelectEventArgs> OnRemove { get; set; }

    /// <summary>Turns the drop zone and the remove buttons off.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The file input's accessible name. Default: "Select files".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The file input's <c>id</c>, for a <c>&lt;label for&gt;</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The files in the list, valid or not.</summary>
    public IReadOnlyList<NxSelectedFile> Files => files;

    /// <summary>Empties the list, without raising <c>OnRemove</c>.</summary>
    public void ClearFiles()
    {
        files.Clear();
        StateHasChanged();
    }

    private string InputId => Id ?? generatedId;
    private string? Hint => FileRules.Hint(extensions, MaxFileSize);
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-fileselect" : $"nx-fileselect {Class}";

    /// <inheritdoc />
    protected override void OnParametersSet() => extensions = FileRules.Normalize(AllowedExtensions);

    private async Task HandleChangeAsync(InputFileChangeEventArgs e)
    {
        if (Disabled)
            return;
        var picked = e.GetMultipleFiles(int.MaxValue)
            .Select(f => new NxSelectedFile(f, FileRules.Check(f.Name, f.Size, extensions, MinFileSize, MaxFileSize)))
            .ToList();
        if (!Multiple)
            picked = picked.Take(1).ToList();
        var pick = ++pickCount;
        var args = new NxFileSelectEventArgs(picked);
        await OnSelect.InvokeAsync(args);
        // A newer pick finished first while OnSelect awaited: it replaced this one.
        if (args.IsCancelled || (!Multiple && pick != pickCount))
            return;
        if (!Multiple)
            files.Clear();
        files.AddRange(picked);
    }

    private async Task RemoveAsync(NxSelectedFile file)
    {
        if (!files.Remove(file))
            return;
        await OnRemove.InvokeAsync(new NxFileSelectEventArgs([file]));
    }
}
