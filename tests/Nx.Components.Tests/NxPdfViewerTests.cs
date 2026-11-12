using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxPdfViewerTests : BunitContext
{
    private static readonly byte[] Pdf = "%PDF-1.4"u8.ToArray();
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-pdfviewer.js");
        module.Mode = JSRuntimeMode.Loose;
        foreach (var path in new[] { "nx-toolbar.js", "nx-combobox.js", "nx-popup.js", "nx-input.js" })
            JSInterop.SetupModule($"./_content/Nx.Components/{path}").Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxPdfViewer> RenderViewer(Action<ComponentParameterCollectionBuilder<NxPdfViewer>>? configure = null) =>
        Render<NxPdfViewer>(p =>
        {
            p.Add(x => x.Id, "pdf");
            configure?.Invoke(p);
        });

    private int Calls(string identifier) => module.Invocations.Count(i => i.Identifier == identifier);

    [Test]
    public void NoDocument_ShowsNoDataText_ToolsOff()
    {
        var cut = RenderViewer(p => p.Add(x => x.NoDataText, "Pick one"));

        Assert.That(cut.Find(".nx-pdfviewer-message").TextContent, Is.EqualTo("Pick one"));
        Assert.That(cut.Find("[data-nx-pdf-tool=next]").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("[data-nx-pdf-tool=download]").HasAttribute("disabled"), Is.True);
        Assert.That(Calls("init"), Is.EqualTo(1));
        Assert.That(Calls("openData") + Calls("openUrl"), Is.Zero);
        Assert.That(cut.Find(".nx-pdfviewer").GetAttribute("style"), Is.EqualTo("height: 600px"));
    }

    [Test]
    public void Data_OpensWithTheZoom_ShowsLoading()
    {
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf).Add(x => x.Zoom, NxPdfViewerZoom.PageFit));

        var open = module.Invocations.Single(i => i.Identifier == "openData");
        Assert.That(open.Arguments[0], Is.EqualTo("pdf"));
        Assert.That(open.Arguments[2], Is.EqualTo("page-fit"));
        Assert.That(cut.Find(".nx-pdfviewer-scroll").GetAttribute("aria-busy"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-loader"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Url_Opens_DataWins()
    {
        RenderViewer(p => p.Add(x => x.Url, "files/a.pdf"));
        Assert.That(module.Invocations.Single(i => i.Identifier == "openUrl").Arguments[1], Is.EqualTo("files/a.pdf"));

        Render<NxPdfViewer>(p => p.Add(x => x.Id, "pdf2").Add(x => x.Url, "files/a.pdf").Add(x => x.Data, Pdf));
        Assert.That(Calls("openData"), Is.EqualTo(1));
        Assert.That(Calls("openUrl"), Is.EqualTo(1));
    }

    [Test]
    public void SameData_DoesNotReopen_NewArrayDoes()
    {
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf));
        cut.Render(p => p.Add(x => x.Data, Pdf));
        Assert.That(Calls("openData"), Is.EqualTo(1));

        cut.Render(p => p.Add(x => x.Data, Pdf.ToArray()));
        Assert.That(Calls("openData"), Is.EqualTo(2));
    }

    [Test]
    public async Task Loaded_EnablesTools_RaisesOnLoad()
    {
        var loaded = new List<int>();
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf).Add(x => x.OnLoad, (int n) => loaded.Add(n)));

        await cut.InvokeAsync(() => cut.Instance.OnLoaded(3));

        Assert.That(loaded, Is.EqualTo(new[] { 3 }));
        Assert.That(cut.Find(".nx-pdfviewer-pages").TextContent, Is.EqualTo("of 3"));
        Assert.That(cut.Find("[data-nx-pdf-tool=previous]").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("[data-nx-pdf-tool=next]").HasAttribute("disabled"), Is.False);
        Assert.That(cut.FindAll(".nx-pdfviewer-message"), Is.Empty);
    }

    [Test]
    public async Task Failed_ShowsTheError_RaisesOnError()
    {
        var errors = new List<string>();
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf).Add(x => x.OnError, (string e) => errors.Add(e)));

        await cut.InvokeAsync(() => cut.Instance.OnFailed("Invalid PDF structure."));

        Assert.That(errors, Is.EqualTo(new[] { "Invalid PDF structure." }));
        var message = cut.Find(".nx-pdfviewer-error");
        Assert.That(message.GetAttribute("role"), Is.EqualTo("alert"));
        Assert.That(message.TextContent, Does.Contain("Invalid PDF structure."));
    }

    [Test]
    public async Task ViewChanged_RaisesPageChanged_ShowsCustomZoom()
    {
        var pages = new List<int>();
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf).Add(x => x.PageChanged, (int n) => pages.Add(n)));
        await cut.InvokeAsync(() => cut.Instance.OnLoaded(3));

        await cut.InvokeAsync(() => cut.Instance.OnViewChanged(2, 1.37, null));
        await cut.InvokeAsync(() => cut.Instance.OnViewChanged(2, 1.37, null));

        Assert.That(pages, Is.EqualTo(new[] { 2 }));
        Assert.That(cut.Find("[data-nx-pdf-tool=previous]").HasAttribute("disabled"), Is.False);
    }

    [Test]
    public async Task OutsidePage_ScrollsToIt()
    {
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf));
        await cut.InvokeAsync(() => cut.Instance.OnLoaded(3));

        cut.Render(p => p.Add(x => x.Page, 3));

        Assert.That(module.Invocations.Single(i => i.Identifier == "setPage").Arguments[1], Is.EqualTo(3));
    }

    [Test]
    public async Task Buttons_CallTheModule()
    {
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf).Add(x => x.FileName, "Report.pdf"));
        await cut.InvokeAsync(() => cut.Instance.OnLoaded(3));

        await cut.Find("[data-nx-pdf-tool=next]").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-nx-pdf-tool=zoomin]").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-nx-pdf-tool=download]").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-nx-pdf-tool=print]").ClickAsync(new MouseEventArgs());

        Assert.That(module.Invocations.Single(i => i.Identifier == "setPage").Arguments[1], Is.EqualTo(2));
        Assert.That(module.Invocations.Single(i => i.Identifier == "zoomStep").Arguments[1], Is.EqualTo(true));
        Assert.That(module.Invocations.Single(i => i.Identifier == "download").Arguments[1], Is.EqualTo("Report.pdf"));
        Assert.That(Calls("print"), Is.EqualTo(1));
    }

    [Test]
    public async Task Matches_ShowTheCount()
    {
        var cut = RenderViewer(p => p.Add(x => x.Data, Pdf));
        await cut.InvokeAsync(() => cut.Instance.OnLoaded(3));
        cut.Find("#pdf-find").Input("slump");
        cut.WaitForState(() => module.Invocations.Any(i => i.Identifier == "find"), TimeSpan.FromSeconds(2));

        await cut.InvokeAsync(() => cut.Instance.OnMatches(1, 3));

        Assert.That(cut.Find(".nx-pdfviewer-matches").TextContent, Is.EqualTo("1 of 3"));
        Assert.That(module.Invocations.First(i => i.Identifier == "find").Arguments[1], Is.EqualTo("slump"));
    }

    [Test]
    public void Tools_Flags_ChooseTheGroups()
    {
        var cut = RenderViewer(p => p.Add(x => x.Tools, NxPdfViewerTools.Download));
        Assert.That(cut.FindAll("[data-nx-pdf-tool]").Select(b => b.GetAttribute("data-nx-pdf-tool")), Is.EqualTo(new[] { "download" }));

        var none = Render<NxPdfViewer>(p => p.Add(x => x.Id, "pdf2").Add(x => x.Tools, NxPdfViewerTools.None));
        Assert.That(none.FindAll("[role=toolbar]"), Is.Empty);
    }

    [Test]
    public void Accessibility_DocumentNamedByFileName()
    {
        var cut = RenderViewer(p => p.Add(x => x.FileName, "Pour report.pdf"));
        var scroll = cut.Find(".nx-pdfviewer-scroll");
        Assert.That(scroll.GetAttribute("role"), Is.EqualTo("document"));
        Assert.That(scroll.GetAttribute("aria-label"), Is.EqualTo("Pour report.pdf"));
        Assert.That(scroll.GetAttribute("tabindex"), Is.EqualTo("0"));
    }
}
