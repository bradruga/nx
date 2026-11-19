using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Sends files to a URL as they are picked: a drop zone (click, drop, or Ctrl+V a screenshot), a list with type and size
/// checks, a progress bar per file, cancel, retry and remove, and the server's answer in <c>OnSuccess</c>. Each file is
/// posted on its own as <c>multipart/form-data</c> under <see cref="SaveField"/>; the browser sends it, not .NET. To read
/// files in .NET instead, use <see cref="NxFileSelect"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxUpload SaveUrl="api/files" AllowedExtensions="allowed" MaxFileSize="25_000_000" OnSuccess="StoreTempId" OnRemove="DeleteTemp" /&gt;
/// </code>
/// </example>
public partial class NxUpload : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-upload.js keeps the File objects and posts them with XHR (progress, cancel, a concurrency limit); it
    // reports picks, progress and results here. The checks, the list and the events are .NET.

    private const string ModulePath = "./_content/Nx.Components/nx-upload.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-upload-{Guid.NewGuid():N}"[..18];
    private readonly List<NxUploadFile> files = [];
    private IReadOnlyList<string> extensions = [];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxUpload>? selfReference;
    private ElementReference input;
    private ElementReference zone;
    private string? pasteMessage;

    /// <summary>The URL each file is posted to.</summary>
    [Parameter, EditorRequired] public string SaveUrl { get; set; } = "";

    /// <summary>The form field the file is posted under. Default: <c>files</c>.</summary>
    [Parameter] public string SaveField { get; set; } = "files";

    /// <summary>Sends cookies and credentials with each request. Default: <c>true</c>.</summary>
    [Parameter] public bool WithCredentials { get; set; } = true;

    /// <summary>Sends files as soon as they are picked; <c>false</c> shows Upload and Clear buttons instead. Default: <c>true</c>.</summary>
    [Parameter] public bool AutoUpload { get; set; } = true;

    /// <summary>The most files sent at once; <c>0</c> for no limit.</summary>
    [Parameter] public int MaxConcurrentUploads { get; set; }

    /// <summary>Lets several files be picked at once. Default: <c>true</c>.</summary>
    [Parameter] public bool Multiple { get; set; } = true;

    /// <summary>The extensions accepted, e.g. <c>.pdf</c>; others are listed as not allowed and never sent. Default: any.</summary>
    [Parameter] public IEnumerable<string>? AllowedExtensions { get; set; }

    /// <summary>The largest file accepted, in bytes. Default: no limit.</summary>
    [Parameter] public long? MaxFileSize { get; set; }

    /// <summary>The smallest file accepted, in bytes. Default: no limit.</summary>
    [Parameter] public long? MinFileSize { get; set; }

    /// <summary>Shows a Paste button that takes files from the clipboard (it asks the browser's permission).</summary>
    [Parameter] public bool ShowPasteButton { get; set; }

    /// <summary>Turns picking, retrying and removing off.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Raised with the files just picked, before they are listed and sent; set <c>IsCancelled</c> to drop them.</summary>
    [Parameter] public EventCallback<NxUploadSelectEventArgs> OnSelect { get; set; }

    /// <summary>Raised when the server answers a file with a 2xx status; its body is in <c>File.ResponseText</c>.</summary>
    [Parameter] public EventCallback<NxUploadEventArgs> OnSuccess { get; set; }

    /// <summary>Raised when a file fails: another status, or no answer.</summary>
    [Parameter] public EventCallback<NxUploadEventArgs> OnError { get; set; }

    /// <summary>Raised when the user cancels a file.</summary>
    [Parameter] public EventCallback<NxUploadEventArgs> OnCancel { get; set; }

    /// <summary>Raised when the user removes a file from the list, e.g. to delete what the server stored.</summary>
    [Parameter] public EventCallback<NxUploadEventArgs> OnRemove { get; set; }

    /// <summary>The file input's accessible name. Default: "Select files to upload".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The file input's <c>id</c>. Generated when not set; the buttons are <c>{Id}-paste</c>, <c>-upload</c>, <c>-clear</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The files in the list, in the order picked.</summary>
    public IReadOnlyList<NxUploadFile> Files => files;

    /// <summary>A file is being sent or waits in the queue.</summary>
    public bool IsUploading => files.Any(f => f.Status == NxUploadStatus.Uploading || (AutoUpload && f.Status == NxUploadStatus.Waiting));

    private string InputId => Id ?? generatedId;
    private string? Hint => FileRules.Hint(extensions, MaxFileSize);
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-upload" : $"nx-upload {Class}";

    private bool CanCancel(NxUploadFile file) =>
        file.Status == NxUploadStatus.Uploading || (AutoUpload && file.Status == NxUploadStatus.Waiting);

    private static string? StatusText(NxUploadFile file) => file.Status switch
    {
        NxUploadStatus.Waiting   => "Waiting",
        NxUploadStatus.Uploading => $"{file.Progress}%",
        NxUploadStatus.Uploaded  => "Uploaded",
        NxUploadStatus.Cancelled => "Cancelled",
        _                        => null
    };

    private string? ErrorText(NxUploadFile file) =>
        file.Error is { } error ? FileRules.Message(error, MinFileSize, MaxFileSize)
        : file.Status == NxUploadStatus.Failed ? (file.StatusCode == 0 ? "Upload failed: no answer from the server." : $"Upload failed ({file.StatusCode}).")
        : null;

    private static string? ToneOf(NxUploadFile file) => file.Status == NxUploadStatus.Uploaded ? "success" : null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet() => extensions = FileRules.Normalize(AllowedExtensions);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        selfReference = DotNetObjectReference.Create(this);
        if (module is not null)
            await module.InvokeVoidAsync("init", input, zone, selfReference);
    }

    // ── From nx-upload.js ─────────────────────────────────────────────────────

    /// <summary>A file nx-upload.js holds: its id, name and size.</summary>
    public sealed record PickedFile(string Id, string Name, long Size);

    /// <summary>Called by nx-upload.js with files picked, dropped or pasted. Not for application use.</summary>
    [JSInvokable]
    public Task OnFilesAdded(PickedFile[] picked) => InvokeAsync(async () =>
    {
        if (Disabled)
            return;
        var added = picked
            .Take(Multiple ? int.MaxValue : 1)
            .Select(p => new NxUploadFile(p.Id, p.Name, p.Size, FileRules.Check(p.Name, p.Size, extensions, MinFileSize, MaxFileSize)))
            .ToList();
        var args = new NxUploadSelectEventArgs(added);
        await OnSelect.InvokeAsync(args);
        if (args.IsCancelled)
        {
            foreach (var file in added)
                await ForgetAsync(file);
            StateHasChanged();
            return;
        }
        if (!Multiple)
        {
            foreach (var old in files.ToList())
                await RemoveAsync(old);
        }
        files.AddRange(added);
        foreach (var file in added.Where(f => f.Error is not null))
            await ForgetAsync(file);
        if (AutoUpload)
            await SendAsync(added.Where(f => f.Status == NxUploadStatus.Waiting));
        StateHasChanged();
    });

    /// <summary>Called by nx-upload.js as a file is sent. Not for application use.</summary>
    [JSInvokable]
    public Task OnUploadProgress(string id, int percent) => InvokeAsync(() =>
    {
        if (Find(id) is { Status: NxUploadStatus.Uploading } file)
        {
            file.Progress = Math.Clamp(percent, 0, 100);
            StateHasChanged();
        }
    });

    /// <summary>Called by nx-upload.js when the server has answered a file, or could not be reached. Not for application use.</summary>
    [JSInvokable]
    public Task OnUploadDone(string id, int status, string responseText) => InvokeAsync(async () =>
    {
        if (Find(id) is not { Status: NxUploadStatus.Uploading } file)
            return;
        file.StatusCode = status;
        file.ResponseText = responseText;
        var ok = status is >= 200 and < 300;
        file.Status = ok ? NxUploadStatus.Uploaded : NxUploadStatus.Failed;
        file.Progress = ok ? 100 : file.Progress;
        StateHasChanged();
        if (ok)
            await OnSuccess.InvokeAsync(new NxUploadEventArgs(file));
        else
            await OnError.InvokeAsync(new NxUploadEventArgs(file));
    });

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>Sends every waiting file (without <see cref="AutoUpload"/>, what the Upload button does).</summary>
    public Task UploadFilesAsync() => SendAsync(files.Where(f => f.Status == NxUploadStatus.Waiting));

    /// <summary>Takes files from the clipboard, as the Paste button does. <c>false</c> when there were none (or the browser refused).</summary>
    public async Task<bool> PasteAsync()
    {
        if (module is null || Disabled)
            return false;
        var any = await module.InvokeAsync<bool>("paste", input);
        pasteMessage = any ? null : "Nothing to paste.";
        StateHasChanged();
        return any;
    }

    /// <summary>Cancels what is being sent and empties the list, without raising <c>OnRemove</c>.</summary>
    public async Task ClearFilesAsync()
    {
        foreach (var file in files)
            await ForgetAsync(file);
        files.Clear();
        StateHasChanged();
    }

    private async Task SendAsync(IEnumerable<NxUploadFile> toSend)
    {
        var batch = toSend.ToList();
        if (batch.Count == 0 || module is null)
            return;
        foreach (var file in batch)
        {
            file.Status = NxUploadStatus.Uploading;
            file.Progress = 0;
            file.StatusCode = 0;
            file.ResponseText = null;
        }
        await module.InvokeVoidAsync("upload", input, batch.Select(f => f.Id).ToArray(), SaveUrl, SaveField, WithCredentials, MaxConcurrentUploads);
    }

    private async Task CancelAsync(NxUploadFile file)
    {
        if (!CanCancel(file))
            return;
        if (module is not null)
            await module.InvokeVoidAsync("cancel", input, file.Id);
        file.Status = NxUploadStatus.Cancelled;
        await OnCancel.InvokeAsync(new NxUploadEventArgs(file));
    }

    private Task RetryAsync(NxUploadFile file) =>
        Disabled || file.Status is not (NxUploadStatus.Failed or NxUploadStatus.Cancelled) ? Task.CompletedTask : SendAsync([file]);

    private async Task RemoveAsync(NxUploadFile file)
    {
        if (!files.Remove(file))
            return;
        await ForgetAsync(file);
        await OnRemove.InvokeAsync(new NxUploadEventArgs(file));
    }

    private async Task ForgetAsync(NxUploadFile file)
    {
        if (module is not null)
            await module.InvokeVoidAsync("forget", input, file.Id);
    }

    private NxUploadFile? Find(string id) => files.FirstOrDefault(f => f.Id == id);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", input);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
