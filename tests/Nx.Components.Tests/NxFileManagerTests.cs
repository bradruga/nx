using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxFileManagerTests : BunitContext
{
    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        foreach (var path in new[] { "nx-input.js", "nx-toolbar.js", "nx-combobox.js", "nx-popup.js" })
            JSInterop.SetupModule($"./_content/Nx.Components/{path}").Mode = JSRuntimeMode.Loose;
    }

    private sealed class Entry
    {
        public string Id { get; init; } = "";
        public string? ParentId { get; init; }
        public string Name { get; init; } = "";
        public string? Extension { get; init; }
        public bool IsDirectory { get; init; }
        public long Size { get; init; }
        public DateTime DateModified { get; init; }
    }

    private static readonly Entry[] Files =
    [
        new() { Id = "d1", Name = "Drawings", IsDirectory = true },
        new() { Id = "d2", ParentId = "d1", Name = "Shop", IsDirectory = true },
        new() { Id = "f1", ParentId = "d2", Name = "WP-12", Extension = ".pdf", Size = 2048, DateModified = new(2026, 10, 2) },
        new() { Id = "f2", ParentId = "d2", Name = "WP-2", Extension = ".pdf", Size = 4096, DateModified = new(2026, 10, 1) },
        new() { Id = "f3", ParentId = "d1", Name = "Plan", Extension = ".pdf", Size = 1024, DateModified = new(2026, 10, 3) },
        new() { Id = "f4", Name = "Contract", Extension = ".docx", Size = 512, DateModified = new(2026, 9, 1) },
        new() { Id = "d3", Name = "Archive", IsDirectory = true },
    ];

    private static void Lambdas(ComponentParameterCollectionBuilder<NxFileManager<Entry>> p)
    {
        p.Add(x => x.ItemId, e => e.Id);
        p.Add(x => x.ParentId, e => e.ParentId);
        p.Add(x => x.ItemName, e => e.Name);
        p.Add(x => x.ItemExtension, e => e.Extension);
        p.Add(x => x.ItemIsDirectory, e => e.IsDirectory);
        p.Add(x => x.ItemSize, e => e.Size);
        p.Add(x => x.ItemDateModified, e => e.DateModified);
    }

    private IRenderedComponent<NxFileManager<Entry>> RenderFiles(Action<ComponentParameterCollectionBuilder<NxFileManager<Entry>>>? configure = null) =>
        Render<NxFileManager<Entry>>(p =>
        {
            p.Add(x => x.Id, "fm");
            p.Add(x => x.Data, Files);
            Lambdas(p);
            p.Add(x => x.Culture, System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            configure?.Invoke(p);
        });

    private static string[] Names(IRenderedComponent<NxFileManager<Entry>> cut) =>
        [.. cut.FindAll("[role=option] .nx-filemanager-name-text").Select(e => e.TextContent)];

    [Test]
    public void Top_FoldersFirst_ThenFiles()
    {
        var cut = RenderFiles();

        Assert.That(Names(cut), Is.EqualTo(new[] { "Archive", "Drawings", "Contract.docx" }));
        Assert.That(cut.Find("[role=listbox]").GetAttribute("aria-label"), Is.EqualTo("Files"));
        Assert.That(cut.Find(".nx-filemanager-crumb").GetAttribute("aria-current"), Is.EqualTo("page"));
    }

    [Test]
    public async Task DoubleClick_Folder_Navigates_File_Opens()
    {
        Entry? folder = null, opened = null;
        var cut = RenderFiles(p => p
            .Add(x => x.FolderChanged, (Entry? f) => folder = f)
            .Add(x => x.OnOpen, (Entry e) => opened = e));

        await cut.FindAll("[role=option]")[1].DoubleClickAsync(new MouseEventArgs());
        Assert.That(folder!.Name, Is.EqualTo("Drawings"));
        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));
        Assert.That(cut.FindAll(".nx-filemanager-crumb").Select(c => c.TextContent), Is.EqualTo(new[] { "Files", "Drawings" }));

        await cut.FindAll("[role=option]")[1].DoubleClickAsync(new MouseEventArgs());
        Assert.That(opened!.Name, Is.EqualTo("Plan"));
    }

    [Test]
    public async Task Click_Selects_DetailsShowIt()
    {
        Entry? selected = null;
        var cut = RenderFiles(p => p.Add(x => x.SelectedItemChanged, (Entry? e) => selected = e));

        await cut.FindAll("[role=option]")[2].ClickAsync(new MouseEventArgs());

        Assert.That(selected!.Name, Is.EqualTo("Contract"));
        Assert.That(cut.FindAll("[role=option]")[2].GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.Find("[role=listbox]").GetAttribute("aria-activedescendant"), Is.EqualTo(cut.FindAll("[role=option]")[2].Id));
        Assert.That(cut.Find(".nx-filemanager-details-name").TextContent, Is.EqualTo("Contract.docx"));
        Assert.That(cut.Find(".nx-filemanager-details dl").TextContent, Does.Contain("DOCX file").And.Contain("512 B"));
    }

    [Test]
    public void Folder_FromOutside_OpensIt()
    {
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]));
        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-2.pdf", "WP-12.pdf" }));
        Assert.That(cut.FindAll(".nx-filemanager-crumb").Select(c => c.TextContent), Is.EqualTo(new[] { "Files", "Drawings", "Shop" }));
    }

    [Test]
    public async Task Sort_BySize_Descending_FoldersStayFirst()
    {
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[0]));

        await cut.FindAll(".nx-filemanager-th")[3].ClickAsync(new MouseEventArgs());
        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));
        cut.Render(p => p.Add(x => x.Folder, Files[1]));
        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-12.pdf", "WP-2.pdf" }));
        await cut.FindAll(".nx-filemanager-th")[3].ClickAsync(new MouseEventArgs());
        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-2.pdf", "WP-12.pdf" }));
        Assert.That(cut.FindAll(".nx-filemanager-th")[3].TextContent, Is.EqualTo("Size ↓"));
    }

    [Test]
    public void Name_SortsNumbersAsNumbers()
    {
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]));
        // WP-2 before WP-12, as Explorer.
        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-2.pdf", "WP-12.pdf" }));
    }

    [Test]
    public void Search_FindsBelowTheOpenFolder_WithLocation()
    {
        var cut = RenderFiles();
        cut.InvokeAsync(() => cut.Find("#fm-search").Input("wp"));
        cut.WaitForState(() => Names(cut).Length == 2, TimeSpan.FromSeconds(2));

        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-2.pdf", "WP-12.pdf" }));
        Assert.That(cut.Find(".nx-filemanager-location").TextContent, Is.EqualTo("in Drawings › Shop"));
        Assert.That(cut.Find(".nx-filemanager-crumb-search").TextContent, Is.EqualTo("Search results"));

        cut.InvokeAsync(() => cut.Find("#fm-search").Input("nothing like it"));
        cut.WaitForState(() => cut.FindAll(".nx-filemanager-empty").Count == 1, TimeSpan.FromSeconds(2));
        Assert.That(cut.Find(".nx-filemanager-empty").TextContent, Is.EqualTo("No files match"));
    }

    [Test]
    public async Task Keys_MoveSelect_EnterOpens_BackspaceGoesUp()
    {
        Entry? opened = null;
        var cut = RenderFiles(p => p.Add(x => x.OnOpen, (Entry e) => opened = e));
        var list = cut.Find("[role=listbox]");

        await list.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await list.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.That(cut.Find("[aria-selected=true] .nx-filemanager-name-text").TextContent, Is.EqualTo("Drawings"));

        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));

        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(opened!.Name, Is.EqualTo("Plan"));

        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "Backspace" });
        Assert.That(Names(cut), Is.EqualTo(new[] { "Archive", "Drawings", "Contract.docx" }));
    }

    [Test]
    public async Task Download_OnlyWithAHandler_ForFiles()
    {
        var none = RenderFiles();
        Assert.That(none.FindAll("[data-nx-filemanager=download]"), Is.Empty);

        Entry? downloaded = null;
        var cut = Render<NxFileManager<Entry>>(p => { Lambdas(p); p.Add(x => x.Id, "fm2").Add(x => x.Data, Files).Add(x => x.OnDownload, (Entry e) => downloaded = e); });
        Assert.That(cut.Find("[data-nx-filemanager=download]").HasAttribute("disabled"), Is.True);
        await cut.FindAll("[role=option]")[2].ClickAsync(new MouseEventArgs());
        await cut.Find("[data-nx-filemanager=download]").ClickAsync(new MouseEventArgs());
        Assert.That(downloaded!.Name, Is.EqualTo("Contract"));
    }

    [Test]
    public async Task View_Toggles_RaisesViewChanged()
    {
        var views = new List<NxFileManagerView>();
        var cut = RenderFiles(p => p.Add(x => x.ViewChanged, (NxFileManagerView v) => views.Add(v)));
        Assert.That(cut.FindAll(".nx-filemanager-grid"), Has.Count.EqualTo(1));

        await cut.Find("[data-nx-filemanager=tiles]").ClickAsync(new MouseEventArgs());

        Assert.That(views, Is.EqualTo(new[] { NxFileManagerView.Tiles }));
        Assert.That(cut.FindAll(".nx-filemanager-tiles"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll(".nx-filemanager-head"), Is.Empty);
    }

    [Test]
    public async Task Tap_OnATouchScreen_Opens()
    {
        Entry? folder = null;
        var cut = RenderFiles(p => p.Add(x => x.FolderChanged, (Entry? f) => folder = f));

        await cut.FindAll("[role=option]")[1].PointerDownAsync(new PointerEventArgs { PointerType = "touch" });
        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());

        Assert.That(folder!.Name, Is.EqualTo("Drawings"));
        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));
    }

    [Test]
    public async Task Click_WithAMouse_OnlySelects()
    {
        var cut = RenderFiles();

        await cut.FindAll("[role=option]")[1].PointerDownAsync(new PointerEventArgs { PointerType = "mouse" });
        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());

        Assert.That(Names(cut), Is.EqualTo(new[] { "Archive", "Drawings", "Contract.docx" }));
        Assert.That(cut.FindAll("[role=option]")[1].GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Breadcrumb_Click_GoesUp()
    {
        Entry? folder = Files[1];
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]).Add(x => x.FolderChanged, (Entry? f) => folder = f));

        await cut.FindAll(".nx-filemanager-crumb")[1].ClickAsync(new MouseEventArgs());
        Assert.That(folder!.Name, Is.EqualTo("Drawings"));
        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));

        await cut.FindAll(".nx-filemanager-crumb")[0].ClickAsync(new MouseEventArgs());
        Assert.That(folder, Is.Null);
        Assert.That(Names(cut), Is.EqualTo(new[] { "Archive", "Drawings", "Contract.docx" }));
    }

    [Test]
    public async Task FolderTree_Click_OpensTheFolder()
    {
        var cut = RenderFiles();

        var drawings = cut.FindAll(".nx-filemanager-tree [role=treeitem] > div").Single(d => d.TextContent.Trim() == "Drawings");
        await drawings.ClickAsync(new MouseEventArgs());

        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));
        Assert.That(cut.FindAll(".nx-filemanager-crumb").Select(c => c.TextContent), Is.EqualTo(new[] { "Files", "Drawings" }));
    }

    [Test]
    public void FolderTree_ShowsOnlyFolders_ExpandedDownToTheOpenOne()
    {
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]));

        var items = cut.FindAll(".nx-filemanager-tree [role=treeitem]");
        Assert.That(items.Select(i => i.QuerySelector(".nx-treeview-content")!.TextContent), Is.EqualTo(new[] { "Drawings", "Shop", "Archive" }));
        Assert.That(items[0].GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(items[1].GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public async Task AltArrowUp_GoesUp_AtTheTopDoesNothing()
    {
        var folders = new List<Entry?>();
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]).Add(x => x.FolderChanged, (Entry? f) => folders.Add(f)));

        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });
        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });
        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "Backspace" });

        Assert.That(folders.Select(f => f?.Name), Is.EqualTo(new[] { "Drawings", null }));
        Assert.That(Names(cut), Is.EqualTo(new[] { "Archive", "Drawings", "Contract.docx" }));
    }

    [Test]
    public async Task Keys_HomeAndArrowUp_StopAtTheFirst()
    {
        var cut = RenderFiles();
        var list = () => cut.Find("[role=listbox]");

        await list().KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Assert.That(cut.Find("[aria-selected=true] .nx-filemanager-name-text").TextContent, Is.EqualTo("Contract.docx"));
        await list().KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        await list().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.That(cut.Find("[aria-selected=true] .nx-filemanager-name-text").TextContent, Is.EqualTo("Archive"));
    }

    [Test]
    public async Task Selection_ScrollsTheSelectedEntryIntoView()
    {
        var module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
        var cut = RenderFiles();

        await cut.Find("[role=listbox]").KeyDownAsync(new KeyboardEventArgs { Key = "End" });

        var reveal = module.Invocations.Where(i => i.Identifier == "reveal").ToList();
        Assert.That(reveal, Has.Count.EqualTo(1));
        Assert.That(reveal[0].Arguments[1], Is.EqualTo(".nx-filemanager-item-selected"));
    }

    [Test]
    public void Search_OnlyBelowTheOpenFolder()
    {
        // Plan.pdf sits in Drawings, above Shop.
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]));
        Assert.That(cut.Find("#fm-search").GetAttribute("placeholder"), Is.EqualTo("Search in Shop"));

        cut.InvokeAsync(() => cut.Find("#fm-search").Input("plan"));
        cut.WaitForState(() => cut.FindAll(".nx-filemanager-empty").Count == 1, TimeSpan.FromSeconds(2));

        Assert.That(cut.Find(".nx-filemanager-empty").TextContent, Is.EqualTo("No files match"));
    }

    [Test]
    public async Task Search_NavigatingClearsIt()
    {
        var cut = RenderFiles();
        await cut.InvokeAsync(() => cut.Find("#fm-search").Input("wp"));
        cut.WaitForState(() => Names(cut).Length == 2, TimeSpan.FromSeconds(2));

        await cut.Find(".nx-filemanager-crumb").ClickAsync(new MouseEventArgs());

        Assert.That(Names(cut), Is.EqualTo(new[] { "Archive", "Drawings", "Contract.docx" }));
        Assert.That(cut.FindAll(".nx-filemanager-crumb-search"), Is.Empty);
    }

    [Test]
    public void Searchable_False_NoSearchBox()
    {
        var cut = RenderFiles(p => p.Add(x => x.Searchable, false));

        Assert.That(cut.FindAll("#fm-search"), Is.Empty);
    }

    [Test]
    public async Task Sort_ByDateModified_AndTheDirectionButton()
    {
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]));

        await cut.FindAll(".nx-filemanager-th")[1].ClickAsync(new MouseEventArgs());
        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-2.pdf", "WP-12.pdf" }));
        Assert.That(cut.FindAll(".nx-filemanager-th")[1].TextContent, Is.EqualTo("Date modified ↑"));

        await cut.Find("[data-nx-filemanager=direction]").ClickAsync(new MouseEventArgs());
        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-12.pdf", "WP-2.pdf" }));
    }

    [Test]
    public async Task Sort_ByName_Descending_FoldersStayFirst()
    {
        var cut = RenderFiles();

        await cut.FindAll(".nx-filemanager-th")[0].ClickAsync(new MouseEventArgs());

        Assert.That(Names(cut), Is.EqualTo(new[] { "Drawings", "Archive", "Contract.docx" }));
    }

    [Test]
    public async Task Details_Folder_CountsWhatItContains()
    {
        var cut = RenderFiles();

        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find(".nx-filemanager-details dl").TextContent, Does.Contain("Folder").And.Contain("1 folder, 1 file"));
    }

    [Test]
    public void Details_NothingSelected_ShowsTheOpenFolder()
    {
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[0]));

        Assert.That(cut.Find(".nx-filemanager-details-none").TextContent, Is.EqualTo("Drawings1 folder, 1 file"));
    }

    [Test]
    public void RootName_NamesTheTop()
    {
        var cut = RenderFiles(p => p.Add(x => x.RootName, "Project"));

        Assert.That(cut.Find(".nx-filemanager-crumb").TextContent, Is.EqualTo("Project"));
        Assert.That(cut.Find("[role=listbox]").GetAttribute("aria-label"), Is.EqualTo("Project"));
        Assert.That(cut.Find(".nx-filemanager-details-none").TextContent, Does.StartWith("Project"));
    }

    [Test]
    public void Details_NothingSelected_AtTheTop_CountsTheTop()
    {
        var cut = RenderFiles();

        Assert.That(cut.Find(".nx-filemanager-details-none").TextContent, Is.EqualTo("Files2 folders, 1 file"));
    }

    [Test]
    public async Task Details_File_ShowsItsLocation_DownloadButtonRaises()
    {
        Entry? downloaded = null;
        var cut = RenderFiles(p => p.Add(x => x.Folder, Files[1]).Add(x => x.OnDownload, (Entry e) => downloaded = e));

        await cut.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find(".nx-filemanager-details dl").TextContent, Does.Contain("Drawings › Shop").And.Contain("4 KB"));

        await cut.Find(".nx-filemanager-details button").ClickAsync(new MouseEventArgs());
        Assert.That(downloaded!.Name, Is.EqualTo("WP-2"));
    }

    [Test]
    public async Task Details_Toggle_HidesThePane()
    {
        var cut = RenderFiles();

        await cut.Find("[data-nx-filemanager=details]").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-filemanager-details"), Is.Empty);
        Assert.That(cut.Find("[data-nx-filemanager=details]").GetAttribute("aria-pressed"), Is.EqualTo("false"));
    }

    [Test]
    public void View_Tiles_FromTheParameter()
    {
        var cut = RenderFiles(p => p.Add(x => x.View, NxFileManagerView.Tiles));

        Assert.That(cut.FindAll(".nx-filemanager-tiles"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("[data-nx-filemanager=tiles]").GetAttribute("aria-pressed"), Is.EqualTo("true"));
    }

    [Test]
    public void SelectedItem_FromOutside_SelectsIt()
    {
        var cut = RenderFiles(p => p.Add(x => x.SelectedItem, Files[5]));

        Assert.That(cut.Find("[aria-selected=true] .nx-filemanager-name-text").TextContent, Is.EqualTo("Contract.docx"));
    }

    private static Entry[] Reloaded() => [.. Files.Select(f => new Entry { Id = f.Id, ParentId = f.ParentId, Name = f.Name,
        Extension = f.Extension, IsDirectory = f.IsDirectory, Size = f.Size, DateModified = f.DateModified })];

    [Test]
    public async Task ParentRender_WithoutBind_KeepsTheUsersFolderAndSelection()
    {
        var cut = RenderFiles();
        await cut.FindAll("[role=option]")[1].DoubleClickAsync(new MouseEventArgs());
        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());

        cut.Render(p => p.Add(x => x.Data, Reloaded()));

        Assert.That(Names(cut), Is.EqualTo(new[] { "Shop", "Plan.pdf" }));
        Assert.That(cut.Find("[aria-selected=true] .nx-filemanager-name-text").TextContent, Is.EqualTo("Plan.pdf"));
    }

    [Test]
    public async Task DataReloaded_KeepsTheOpenFolderAndSelection()
    {
        IRenderedComponent<NxFileManager<Entry>>? cut = null;
        cut = RenderFiles(p => p
            .Add(x => x.Folder, Files[1])
            .Add(x => x.SelectedItemChanged, (Entry? e) => cut?.Render(q => q.Add(x => x.SelectedItem, e))));
        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());

        // ERP reloads the list: new objects, same ids.
        cut.Render(p => p.Add(x => x.Data, Reloaded()));

        Assert.That(Names(cut), Is.EqualTo(new[] { "WP-2.pdf", "WP-12.pdf" }));
        Assert.That(cut.Find("[aria-selected=true] .nx-filemanager-name-text").TextContent, Is.EqualTo("WP-12.pdf"));
    }

    [Test]
    public void Empty_And_MissingLambda()
    {
        var cut = Render<NxFileManager<Entry>>(p => { Lambdas(p); p.Add(x => x.Data, Array.Empty<Entry>()).Add(x => x.EmptyText, "Nothing yet"); });
        Assert.That(cut.Find(".nx-filemanager-empty").TextContent, Is.EqualTo("Nothing yet"));

        Assert.Throws<InvalidOperationException>(() =>
            Render<NxFileManager<Entry>>(p => p.Add(x => x.Data, Files).Add(x => x.ItemId, e => e.Id).Add(x => x.ItemName, e => e.Name)));
    }
}
