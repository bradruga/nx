using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Browses a read-only tree of folders and files: a folder tree, a breadcrumb, the open folder as a grid or tiles, sort
/// (folders always first, as Explorer), a details pane, and a search that matches anywhere in a name below the open
/// folder. Double-click, Enter, or a tap on a touch screen opens: folders navigate, files raise <see cref="OnOpen"/>.
/// Bound to a flat list with lambdas (<c>ItemId</c>, <c>ParentId</c>, <c>ItemName</c>, <c>ItemIsDirectory</c>…).
/// </summary>
/// <typeparam name="TItem">The entry type.</typeparam>
/// <example>
/// <code>
/// &lt;NxFileManager Data="entries" ItemId="e =&gt; e.Id" ParentId="e =&gt; e.ParentId" ItemName="e =&gt; e.Name"
///                ItemIsDirectory="e =&gt; e.IsDirectory" @bind-Folder="folder" @bind-SelectedItem="selected"
///                OnOpen="OpenFileAsync" OnDownload="DownloadAsync" Height="100%" /&gt;
/// </code>
/// </example>
public partial class NxFileManager<TItem> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js keeps the list's keys from scrolling the page and scrolls the selected entry into view.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";
    private static readonly string[] ListKeys = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End"];
    private static readonly NxFileManagerSort[] SortChoices = Enum.GetValues<NxFileManagerSort>();

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-fm-{Guid.NewGuid():N}"[..14];
    private IJSObjectReference? module;
    private ElementReference list;
    private bool keysApplied, revealPending;

    private Dictionary<object, Node> byId = [];
    private List<Node> roots = [];
    private Node? current;
    private Node? selected;
    private readonly HashSet<object> expandedIds = [];
    private object? lastFolder, lastSelected;
    private bool initialized;
    private NxFileManagerView view;
    private NxFileManagerSort sort;
    private bool descending;
    private bool showDetails = true;
    private string? query;
    private string? pointerType;

    /// <summary>One entry.</summary>
    private sealed class Node(TItem item, object id, object? parentId, string name, string? extension, bool isDirectory)
    {
        public TItem Item { get; } = item;
        public object Id { get; } = id;
        public object? ParentId { get; } = parentId;
        public string Name { get; } = name;
        public string? Extension { get; } = extension;
        public bool IsDirectory { get; } = isDirectory;
        public long? Size { get; init; }
        public DateTime? Modified { get; init; }
        public DateTime? Created { get; init; }
        public Node? Parent { get; set; }
        public int Index { get; set; }
        public List<Node> Children { get; } = [];
        public string DisplayName => IsDirectory || string.IsNullOrEmpty(Extension) ? Name : Name + Extension;
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>Every folder and file, flat: each names its parent by id.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>An entry's unique id. Required.</summary>
    [Parameter, EditorRequired] public Func<TItem, object>? ItemId { get; set; }

    /// <summary>Its parent folder's id; <c>null</c> (or not passed) at the top.</summary>
    [Parameter] public Func<TItem, object?>? ParentId { get; set; }

    /// <summary>Its name, without the extension for a file. Required.</summary>
    [Parameter, EditorRequired] public Func<TItem, string>? ItemName { get; set; }

    /// <summary>A file's extension (<c>.pdf</c>).</summary>
    [Parameter] public Func<TItem, string?>? ItemExtension { get; set; }

    /// <summary>Tells a folder from a file. Required.</summary>
    [Parameter, EditorRequired] public Func<TItem, bool>? ItemIsDirectory { get; set; }

    /// <summary>A file's size in bytes.</summary>
    [Parameter] public Func<TItem, long?>? ItemSize { get; set; }

    /// <summary>When it was modified.</summary>
    [Parameter] public Func<TItem, DateTime?>? ItemDateModified { get; set; }

    /// <summary>When it was created.</summary>
    [Parameter] public Func<TItem, DateTime?>? ItemDateCreated { get; set; }

    // ── Where and what ────────────────────────────────────────────────────────

    /// <summary>The open folder; <c>null</c> for the top. Use <c>@bind-Folder</c>.</summary>
    [Parameter] public TItem? Folder { get; set; }

    /// <summary>Raised when the user opens another folder.</summary>
    [Parameter] public EventCallback<TItem?> FolderChanged { get; set; }

    /// <summary>The selected entry. Use <c>@bind-SelectedItem</c>.</summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    /// <summary>Grid or tiles at first; the toolbar switches. Use <c>@bind-View</c> to keep it.</summary>
    [Parameter] public NxFileManagerView View { get; set; }

    /// <summary>Raised when the toolbar switches the view.</summary>
    [Parameter] public EventCallback<NxFileManagerView> ViewChanged { get; set; }

    /// <summary>Raised when a file is opened (double-click, Enter, a tap on a touch screen).</summary>
    [Parameter] public EventCallback<TItem> OnOpen { get; set; }

    /// <summary>Raised by the Download buttons; with no handler they are not shown.</summary>
    [Parameter] public EventCallback<TItem> OnDownload { get; set; }

    // ── Look ──────────────────────────────────────────────────────────────────

    /// <summary>A search box that finds names containing the text, anywhere below the open folder. Default: <c>true</c>.</summary>
    [Parameter] public bool Searchable { get; set; } = true;

    /// <summary>The top folder's name in the breadcrumb. Default: "Files".</summary>
    [Parameter] public string RootName { get; set; } = "Files";

    /// <summary>Shown in an empty folder. Default: "This folder is empty".</summary>
    [Parameter] public string EmptyText { get; set; } = "This folder is empty";

    /// <summary>Shown when a search finds nothing. Default: "No files match".</summary>
    [Parameter] public string NoResultsText { get; set; } = "No files match";

    /// <summary>Formats dates. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>The file manager's height. Default: <c>500px</c>; <c>100%</c> fills a container with a height.</summary>
    [Parameter] public string? Height { get; set; } = "500px";

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes go to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string BaseId => Id ?? generatedId;
    private string OptionId(Node node) => $"{BaseId}-e{node.Index}";
    private bool Searching => Searchable && !string.IsNullOrWhiteSpace(query);
    private CultureInfo Formats => Culture ?? CultureInfo.CurrentCulture;
    private string SearchPlaceholder => $"Search in {(current is null ? RootName : current.Name)}";
    private string ListLabel => Searching ? "Search results" : current is null ? RootName : current.Name;
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-filemanager" : $"nx-filemanager {Class}";

    private string? OuterStyle
    {
        get
        {
            var height = string.IsNullOrEmpty(Height) ? null : $"height: {Height}";
            return string.IsNullOrEmpty(Style) ? height : height is null ? Style : $"{height}; {Style}";
        }
    }

    private static RenderFragment Icon(NxIconData icon) => builder =>
    {
        builder.OpenComponent<NxIcon>(0);
        builder.AddComponentParameter(1, nameof(NxIcon.Icon), icon);
        builder.CloseComponent();
    };

    private static string SortName(NxFileManagerSort s) => s switch
    {
        NxFileManagerSort.DateModified => "Date modified",
        _ => s.ToString()
    };

    private string SortMark(NxFileManagerSort s) => s != sort ? "" : descending ? " ↓" : " ↑";

    private static NxIconData IconFor(Node node) => node.IsDirectory ? NxIcons.Folder : (node.Extension ?? "").ToLowerInvariant() switch
    {
        ".pdf" => NxIcons.FilePdf,
        ".doc" or ".docx" or ".rtf" => NxIcons.FileWord,
        ".xls" or ".xlsx" or ".csv" => NxIcons.FileExcel,
        ".txt" or ".log" => NxIcons.FileTxt,
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".webp" => NxIcons.FileImage,
        _ => NxIcons.File
    };

    private static string TypeText(Node node) =>
        node.IsDirectory ? "Folder" : string.IsNullOrEmpty(node.Extension) ? "File" : $"{node.Extension.TrimStart('.').ToUpperInvariant()} file";

    private string DateText(DateTime? date) => date?.ToString("g", Formats) ?? "";
    private static string SizeText(long? size) => size is { } bytes ? FileRules.Size(bytes) : "";

    private string CountText(Node? folder)
    {
        var children = folder is null ? roots : folder.Children;
        var folders = children.Count(c => c.IsDirectory);
        var files = children.Count - folders;
        return $"{folders} folder{(folders == 1 ? "" : "s")}, {files} file{(files == 1 ? "" : "s")}";
    }

    private string LocationOf(Node node)
    {
        var path = Ancestors(node.Parent, includeSelf: true).Select(n => n.Name).ToList();
        return path.Count == 0 ? RootName : string.Join(" › ", path);
    }

    /// <summary>The folders from the top down to <paramref name="node"/>.</summary>
    private static IEnumerable<Node> Ancestors(Node? node, bool includeSelf)
    {
        var chain = new List<Node>();
        for (var n = includeSelf ? node : node?.Parent; n is not null; n = n.Parent)
            chain.Add(n);
        chain.Reverse();
        return chain;
    }

    // ── The folder tree (an NxTreeView of the folders) ────────────────────────

    private object IdOf(TItem item) => ItemId!(item);
    private object? ParentOf(TItem item) => ParentId?.Invoke(item);
    private string NameOf(TItem item) => ItemName!(item) ?? "";
    private TItem? CurrentItem => current is null ? default : current.Item;
    private IEnumerable<TItem> FolderItems => byId.Values.Where(n => n.IsDirectory).Select(n => n.Item);
    private IEnumerable<TItem> ExpandedFolderItems => expandedIds.Where(byId.ContainsKey).Select(id => byId[id].Item).ToList();

    private Task TreeSelectAsync(TItem? item) =>
        NavigateAsync(item is null ? null : byId.GetValueOrDefault(IdOf(item)));

    private void TreeExpanded(IEnumerable<TItem> items)
    {
        expandedIds.Clear();
        foreach (var item in items)
            expandedIds.Add(IdOf(item));
    }

    // ── What the list shows ───────────────────────────────────────────────────

    private List<Node> Shown()
    {
        IEnumerable<Node> entries;
        if (Searching)
        {
            var term = query!.Trim();
            var found = new List<Node>();
            void Walk(IEnumerable<Node> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        found.Add(node);
                    Walk(node.Children);
                }
            }
            Walk(current?.Children ?? roots);
            entries = found;
        }
        else
        {
            entries = current?.Children ?? roots;
        }
        return Sorted(entries);
    }

    // Folders first, as Explorer; then by the chosen field, the name breaking ties.
    private List<Node> Sorted(IEnumerable<Node> entries)
    {
        var folders = entries.OrderBy(n => n.IsDirectory ? 0 : 1);
        IOrderedEnumerable<Node> ordered = sort switch
        {
            NxFileManagerSort.DateModified => descending ? folders.ThenByDescending(n => n.Modified) : folders.ThenBy(n => n.Modified),
            NxFileManagerSort.Type => descending ? folders.ThenByDescending(n => n.Extension, StringComparer.OrdinalIgnoreCase) : folders.ThenBy(n => n.Extension, StringComparer.OrdinalIgnoreCase),
            NxFileManagerSort.Size => descending ? folders.ThenByDescending(n => n.Size) : folders.ThenBy(n => n.Size),
            _ => folders
        };
        var byName = StringComparer.Create(Formats, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
        ordered = sort == NxFileManagerSort.Name && descending
            ? ordered.ThenByDescending(n => n.DisplayName, byName)
            : ordered.ThenBy(n => n.DisplayName, byName);
        return [.. ordered];
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ItemId is null || ItemName is null || ItemIsDirectory is null)
            throw new InvalidOperationException("NxFileManager needs ItemId, ItemName and ItemIsDirectory.");
        Build();
        if (!initialized)
            view = View;

        // Only an outside change moves the folder or the selection, as the other Nx two-way parameters.
        var folderId = Folder is null ? null : IdOf(Folder);
        if (!initialized || !Equals(folderId, lastFolder))
        {
            lastFolder = folderId;
            current = folderId is null ? null : byId.GetValueOrDefault(folderId);
            Reveal(current);
        }
        else if (current is not null)
        {
            current = byId.GetValueOrDefault(current.Id);
        }

        var selectedId = SelectedItem is null ? null : IdOf(SelectedItem);
        if (!initialized || !Equals(selectedId, lastSelected))
        {
            lastSelected = selectedId;
            selected = selectedId is null ? null : byId.GetValueOrDefault(selectedId);
        }
        else if (selected is not null)
        {
            selected = byId.GetValueOrDefault(selected.Id);
        }
        initialized = true;
    }

    /// <summary>Builds the tree from the flat list on every parameter set: ERP reloads it in place.</summary>
    private void Build()
    {
        var map = new Dictionary<object, Node>();
        foreach (var item in Data ?? [])
        {
            if (item is null)
                continue;
            var id = IdOf(item);
            var node = new Node(item, id, ParentOf(item), NameOf(item), ItemExtension?.Invoke(item), ItemIsDirectory!(item))
            {
                Size = ItemSize?.Invoke(item),
                Modified = ItemDateModified?.Invoke(item),
                Created = ItemDateCreated?.Invoke(item)
            };
            node.Index = map.Count;
            map.TryAdd(id, node);
        }
        var top = new List<Node>();
        foreach (var node in map.Values)
        {
            if (node.ParentId is not null && map.TryGetValue(node.ParentId, out var parent) && parent != node)
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
            else
            {
                top.Add(node);
            }
        }
        byId = map;
        roots = top;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        if (!keysApplied)
        {
            keysApplied = true;
            await module.InvokeVoidAsync("preventKeys", list, ListKeys);
        }
        if (revealPending)
        {
            revealPending = false;
            await module.InvokeVoidAsync("reveal", list, ".nx-filemanager-item-selected");
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    // Opens the folders down to it in the tree, so the tree shows where the user stands.
    private void Reveal(Node? folder)
    {
        foreach (var ancestor in Ancestors(folder, includeSelf: false))
            expandedIds.Add(ancestor.Id);
    }

    private async Task NavigateAsync(Node? folder)
    {
        query = null;
        current = folder;
        Reveal(folder);
        await SelectAsync(null);
        // Unbound, the parameter stays as it was: remembering the raised id would make it look like an outside change.
        if (FolderChanged.HasDelegate)
            lastFolder = folder?.Id;
        await FolderChanged.InvokeAsync(folder is null ? default : folder.Item);
    }

    private async Task SelectAsync(Node? node)
    {
        if (node == selected)
            return;
        selected = node;
        if (SelectedItemChanged.HasDelegate)
            lastSelected = node?.Id;
        revealPending = true;
        await SelectedItemChanged.InvokeAsync(node is null ? default : node.Item);
    }

    private async Task ClickAsync(Node node)
    {
        await SelectAsync(node);
        // A touch screen has no double-click: the tap that selects opens.
        if (pointerType is "touch" or "pen")
            await OpenAsync(node);
    }

    private async Task OpenAsync(Node node)
    {
        if (node.IsDirectory)
            await NavigateAsync(node);
        else
            await OnOpen.InvokeAsync(node.Item);
    }

    private Task DownloadAsync(Node? node) =>
        node is null || node.IsDirectory ? Task.CompletedTask : OnDownload.InvokeAsync(node.Item);

    private async Task SetViewAsync(NxFileManagerView next)
    {
        if (view == next)
            return;
        view = next;
        await ViewChanged.InvokeAsync(next);
    }

    private void SetSort(NxFileManagerSort next)
    {
        if (sort == next)
            descending = !descending;
        else
        {
            sort = next;
            descending = false;
        }
    }

    private void ToggleDirection() => descending = !descending;

    private async Task SearchAsync(string? text)
    {
        query = text;
        await SelectAsync(null);
    }

    private async Task KeyDownAsync(KeyboardEventArgs e)
    {
        // Read the list now: a key that arrives before the last one's render must not see the nodes it replaced.
        var shown = Shown();
        if (e.Key == "Backspace" || (e.AltKey && e.Key == "ArrowUp"))
        {
            if (current is not null)
                await NavigateAsync(current.Parent);
            return;
        }
        if (e.AltKey || e.CtrlKey || e.MetaKey || shown.Count == 0)
            return;
        var at = selected is null ? -1 : shown.IndexOf(selected);
        var next = e.Key switch
        {
            "ArrowDown" or "ArrowRight" => Math.Min(at + 1, shown.Count - 1),
            "ArrowUp" or "ArrowLeft" => Math.Max(at - 1, 0),
            "Home" => 0,
            "End" => shown.Count - 1,
            _ => -2
        };
        if (next >= 0)
            await SelectAsync(shown[next]);
        else if (e.Key == "Enter" && selected is not null)
            await OpenAsync(selected);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;
        try
        {
            if (keysApplied)
                await module.InvokeVoidAsync("dispose", list);
            await module.DisposeAsync();
        }
        catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
