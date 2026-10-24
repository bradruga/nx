using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxToolBarTests : BunitContext
{
    private BunitJSModuleInterop module = default!;
    private readonly List<string> clicks = [];

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-toolbar.js");
        module.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-menu.js").Mode = JSRuntimeMode.Loose;
    }

    private RenderFragment Button(string id, string text, bool disabled = false, bool visible = true) => b =>
    {
        b.OpenComponent<NxToolBarButton>(0);
        b.AddComponentParameter(1, nameof(NxToolBarButton.Id), id);
        b.AddComponentParameter(2, nameof(NxToolBarButton.Text), text);
        b.AddComponentParameter(3, nameof(NxToolBarButton.Disabled), disabled);
        b.AddComponentParameter(4, nameof(NxToolBarButton.Visible), visible);
        b.AddComponentParameter(5, nameof(NxToolBarButton.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks.Add(text)));
        b.CloseComponent();
    };

    private static RenderFragment Separator => b =>
    {
        b.OpenComponent<NxToolBarSeparator>(0);
        b.CloseComponent();
    };

    private IRenderedComponent<NxToolBar> RenderBar(params RenderFragment[] items) =>
        Render<NxToolBar>(p => p
            .Add(x => x.AriaLabel, "Sheet")
            .Add(x => x.ChildContent, b =>
            {
                for (var i = 0; i < items.Length; i++)
                    b.AddContent(i, items[i]);
            }));

    [Test]
    public void Render_ToolbarRole_ButtonsInOrder_JsInit()
    {
        var cut = RenderBar(Button("new", "New"), Separator, Button("save", "Save"));

        var bar = cut.Find("[role=toolbar]");
        Assert.That(bar.GetAttribute("aria-label"), Is.EqualTo("Sheet"));
        var items = cut.FindAll("[data-nx-toolbar-item]");
        Assert.That(items.Select(i => i.TagName), Is.EqualTo(new[] { "BUTTON", "SPAN", "BUTTON" }));
        Assert.That(cut.Find("#new").TextContent.Trim(), Is.EqualTo("New"));
        Assert.That(cut.Find("[role=separator]").GetAttribute("aria-orientation"), Is.EqualTo("vertical"));
        Assert.That(cut.FindAll(".nx-toolbar-more"), Is.Empty);
        Assert.That(module.Invocations.Single(i => i.Identifier == "init").Arguments[1], Is.EqualTo(true));
    }

    [Test]
    public async Task Click_RaisesOnClick()
    {
        var cut = RenderBar(Button("new", "New"));
        await cut.Find("#new").ClickAsync(new MouseEventArgs());
        Assert.That(clicks, Is.EqualTo(new[] { "New" }));
    }

    [Test]
    public void Hidden_AndDisabled()
    {
        var cut = RenderBar(Button("a", "A", disabled: true), Button("b", "B", visible: false));
        Assert.That(cut.Find("#a").HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll("#b"), Is.Empty);
    }

    [Test]
    public async Task Overflow_MovesButtonsIntoTheMoreMenu()
    {
        var cut = RenderBar(Button("new", "New"), Separator, Button("pdf", "Export PDF"), Button("notes", "Notes", disabled: true));
        var separatorKey = cut.Find(".nx-toolbar-separator").GetAttribute("data-nx-toolbar-item")!;

        await cut.InvokeAsync(() => cut.Instance.OnOverflow([separatorKey, "pdf", "notes"]));

        var more = cut.Find(".nx-toolbar-more button");
        Assert.That(more.GetAttribute("aria-label"), Is.EqualTo("More actions"));
        Assert.That(cut.Find("#pdf").ClassList, Does.Contain("nx-toolbar-item-overflow"));
        Assert.That(cut.Find("#pdf").GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(cut.Find("#new").ClassList, Does.Not.Contain("nx-toolbar-item-overflow"));
        Assert.That(cut.Find(".nx-toolbar-separator").ClassList, Does.Contain("nx-toolbar-item-overflow"));

        await more.ClickAsync(new MouseEventArgs());
        var entries = cut.FindAll("[role=menuitem]");
        Assert.That(entries.Select(e => e.TextContent.Trim()), Is.EqualTo(new[] { "Export PDF", "Notes" }), "a leading separator is dropped");
        Assert.That(entries[1].GetAttribute("aria-disabled"), Is.EqualTo("true"));

        await entries[0].ClickAsync(new MouseEventArgs());
        Assert.That(clicks, Is.EqualTo(new[] { "Export PDF" }));
    }

    [Test]
    public async Task Overflow_Wrap_HasNoMenu()
    {
        var cut = Render<NxToolBar>(p => p
            .Add(x => x.Overflow, NxToolBarOverflow.Wrap)
            .Add(x => x.ChildContent, Button("a", "A")));

        await cut.InvokeAsync(() => cut.Instance.OnOverflow(["a"]));

        Assert.That(cut.Find(".nx-toolbar").ClassList, Does.Contain("nx-toolbar-wrap"));
        Assert.That(cut.FindAll(".nx-toolbar-more"), Is.Empty);
        Assert.That(cut.Find("#a").ClassList, Does.Not.Contain("nx-toolbar-item-overflow"));
    }

    [Test]
    public void ToolbarSize_ReachesButtons_UnlessTheirOwn()
    {
        var cut = Render<NxToolBar>(p => p
            .Add(x => x.Size, NxButtonSize.Small)
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxToolBarButton>(0);
                b.AddComponentParameter(1, nameof(NxToolBarButton.Id), "a");
                b.AddComponentParameter(2, nameof(NxToolBarButton.Text), "A");
                b.CloseComponent();
                b.OpenComponent<NxToolBarButton>(3);
                b.AddComponentParameter(4, nameof(NxToolBarButton.Id), "b");
                b.AddComponentParameter(5, nameof(NxToolBarButton.Text), "B");
                b.AddComponentParameter(6, nameof(NxToolBarButton.Size), (NxButtonSize?)NxButtonSize.Large);
                b.AddComponentParameter(7, nameof(NxToolBarButton.Variant), NxButtonVariant.Primary);
                b.CloseComponent();
            }));

        Assert.That(cut.Find("#a").ClassList, Does.Contain("nx-button-sm").And.Contain("nx-button-secondary"));
        Assert.That(cut.Find("#b").ClassList, Does.Contain("nx-button-lg").And.Contain("nx-button-primary"));
    }

    [Test]
    public void Spacer_RendersAnItem()
    {
        var cut = Render<NxToolBar>(p => p.Add(x => x.ChildContent, b =>
        {
            b.OpenComponent<NxToolBarSpacer>(0);
            b.CloseComponent();
        }));
        Assert.That(cut.Find(".nx-toolbar-spacer").HasAttribute("data-nx-toolbar-item"), Is.True);
    }

    [Test]
    public void ButtonOutsideAToolbar_Throws() => Assert.Throws<InvalidOperationException>(() => Render<NxToolBarButton>());
}
