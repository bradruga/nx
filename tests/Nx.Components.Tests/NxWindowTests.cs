using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxWindowTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-window.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Text(string text) => b => b.AddContent(0, text);

    /// <summary>A bound window: VisibleChanged writes back like @bind-Visible.</summary>
    private IRenderedComponent<NxWindow> RenderBound(List<bool> raised, bool visible = true,
        Action<ComponentParameterCollectionBuilder<NxWindow>>? configure = null)
    {
        IRenderedComponent<NxWindow>? cut = null;
        cut = Render<NxWindow>(p =>
        {
            p.Add(x => x.Visible, visible);
            p.Add(x => x.Id, "w");
            p.Add(x => x.Title, "Edit pour");
            p.Add(x => x.ChildContent, Text("Body"));
            p.Add(x => x.VisibleChanged, (bool v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Visible, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Hidden_RendersNothing()
    {
        var cut = RenderBound([], visible: false);

        Assert.That(cut.Markup.Trim(), Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "open"), Is.False);
    }

    [Test]
    public void Visible_ModalDialogLabelledByTitle_WithBackdrop()
    {
        var cut = RenderBound([]);

        var window = cut.Find("#w[role=dialog]");
        Assert.That(window.GetAttribute("aria-modal"), Is.EqualTo("true"));
        Assert.That(window.GetAttribute("aria-labelledby"), Is.EqualTo("w-title"));
        Assert.That(cut.Find("#w-title").TextContent.Trim(), Is.EqualTo("Edit pour"));
        Assert.That(cut.FindAll(".nx-window-backdrop"), Has.Count.EqualTo(1));
        Assert.That(window.ClassList, Does.Contain("nx-window-medium"));
        Assert.That(window.HasAttribute("style"), Is.False);
        Assert.That(cut.Find(".nx-window-body").TextContent, Is.EqualTo("Body"));
    }

    [Test]
    public void NonModal_NoBackdropNoAriaModal()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Modal, false));

        Assert.That(cut.FindAll(".nx-window-backdrop"), Is.Empty);
        Assert.That(cut.Find("#w").HasAttribute("aria-modal"), Is.False);
    }

    [Test]
    public void Open_PassesOptionsToJs()
    {
        RenderBound([], configure: p => p
            .Add(x => x.Width, "720px").Add(x => x.Left, "40px").Add(x => x.Top, "80px").Add(x => x.CloseOnEscape, false));

        var open = module.Invocations.Single(i => i.Identifier == "open");
        Assert.That(open.Arguments[0], Is.EqualTo("w"));
        var options = open.Arguments[4]!;
        string? Get(string name) => options.GetType().GetProperty(name)!.GetValue(options)?.ToString();
        Assert.That(Get("width"), Is.EqualTo("720px"));
        Assert.That(Get("left"), Is.EqualTo("40px"));
        Assert.That(Get("top"), Is.EqualTo("80px"));
        Assert.That(Get("modal"), Is.EqualTo("True"));
        Assert.That(Get("closeOnEscape"), Is.EqualTo("False"));
    }

    [Test]
    public void ExplicitWidth_DropsSizeClass()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Width, "720px").Add(x => x.Size, NxWindowSize.Large));

        Assert.That(cut.Find("#w").ClassList, Does.Not.Contain("nx-window-large"));
    }

    [TestCase(NxWindowSize.Small, "nx-window-small")]
    [TestCase(NxWindowSize.Large, "nx-window-large")]
    [TestCase(NxWindowSize.Auto, "nx-window-auto")]
    public void Size_Class(NxWindowSize size, string css)
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Size, size));

        Assert.That(cut.Find("#w").ClassList, Does.Contain(css));
    }

    [Test]
    public void TitleContentIconAndHeaderActions()
    {
        var cut = RenderBound([], configure: p => p
            .Add(x => x.TitleContent, Text("Rich title"))
            .Add(x => x.Icon, Text("I"))
            .Add(x => x.HeaderActions, b => b.AddMarkupContent(0, "<button class=\"nx-window-action custom\">R</button>")));

        Assert.That(cut.Find("#w-title").TextContent.Trim(), Is.EqualTo("Rich title"));
        Assert.That(cut.Find(".nx-window-icon").TextContent, Is.EqualTo("I"));
        Assert.That(cut.Find(".nx-window-actions .custom"), Is.Not.Null);
    }

    [Test]
    public void Footer_RenderedWhenGiven()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Footer, Text("Buttons")));

        Assert.That(cut.Find(".nx-window-footer").TextContent, Is.EqualTo("Buttons"));
    }

    [Test]
    public void Resizable_RendersEightHandles()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Resizable, true));

        Assert.That(cut.FindAll(".nx-window-resize"), Has.Count.EqualTo(8));
        Assert.That(cut.FindAll(".nx-window-resize").Select(h => h.GetAttribute("data-edge")),
            Is.EquivalentTo(new[] { "n", "s", "e", "w", "ne", "nw", "se", "sw" }));
    }

    [Test]
    public void TitleBarButtons_ByParameter()
    {
        var plain = RenderBound([]);
        Assert.That(plain.FindAll(".nx-window-close"), Has.Count.EqualTo(1));
        Assert.That(plain.FindAll(".nx-window-minimize, .nx-window-maximize"), Is.Empty);

        var all = RenderBound([], configure: p => p.Add(x => x.Minimizable, true).Add(x => x.Maximizable, true).Add(x => x.Closable, false));
        Assert.That(all.FindAll(".nx-window-close"), Is.Empty);
        Assert.That(all.Find(".nx-window-minimize").GetAttribute("aria-label"), Is.EqualTo("Minimize"));
        Assert.That(all.Find(".nx-window-maximize").GetAttribute("aria-label"), Is.EqualTo("Maximize"));
    }

    // ── Closing ───────────────────────────────────────────────────────────────

    [Test]
    public async Task CloseButton_RaisesVisibleChangedAndClosesInJs()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised);

        await cut.Find(".nx-window-close").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { false }));
        Assert.That(cut.FindAll("#w"), Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "close" && (string?)i.Arguments[0] == "w"), Is.True);
    }

    [Test]
    public async Task Escape_FromJs_Closes()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised);

        await cut.InvokeAsync(cut.Instance.OnEscape);

        Assert.That(raised, Is.EqualTo(new[] { false }));
    }

    [Test]
    public void EscapeOption_FollowsClosable()
    {
        RenderBound([], configure: p => p.Add(x => x.Closable, false));

        var options = module.Invocations.Single(i => i.Identifier == "open").Arguments[4]!;
        Assert.That(options.GetType().GetProperty("closeOnEscape")!.GetValue(options), Is.False);
    }

    [Test]
    public async Task Backdrop_ClosesOnlyWhenAsked()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised);
        await cut.Find(".nx-window-backdrop").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.Empty);

        cut.Render(p => p.Add(x => x.CloseOnBackdropClick, true));
        await cut.Find(".nx-window-backdrop").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { false }));
    }

    [Test]
    public async Task OnClosing_CanCancel()
    {
        var raised = new List<bool>();
        var cancel = true;
        var cut = RenderBound(raised, configure: p => p.Add(x => x.OnClosing, (NxWindowClosingEventArgs e) => e.Cancel = cancel));

        await cut.Find(".nx-window-close").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.Empty);
        Assert.That(cut.FindAll("#w"), Has.Count.EqualTo(1));

        cancel = false;
        await cut.Find(".nx-window-close").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { false }));
    }

    [Test]
    public void ParentHides_ClosesInJs()
    {
        var cut = RenderBound([]);

        cut.Render(p => p.Add(x => x.Visible, false));

        Assert.That(cut.FindAll("#w"), Is.Empty);
        Assert.That(module.Invocations.Count(i => i.Identifier == "close"), Is.EqualTo(1));
    }

    [Test]
    public void Reopen_OpensInJsAgain()
    {
        var cut = RenderBound([]);

        cut.Render(p => p.Add(x => x.Visible, false));
        cut.Render(p => p.Add(x => x.Visible, true));

        Assert.That(module.Invocations.Count(i => i.Identifier == "open"), Is.EqualTo(2));
    }

    // ── Minimize and maximize ─────────────────────────────────────────────────

    [Test]
    public async Task Minimize_HidesBodyKeepsIt_RaisesChanged()
    {
        var minimized = new List<bool>();
        var cut = RenderBound([], configure: p => p
            .Add(x => x.Minimizable, true)
            .Add(x => x.Footer, Text("F"))
            .Add(x => x.MinimizedChanged, (bool v) => minimized.Add(v)));

        await cut.Find(".nx-window-minimize").ClickAsync(new MouseEventArgs());

        Assert.That(minimized, Is.EqualTo(new[] { true }));
        Assert.That(cut.Find("#w").ClassList, Does.Contain("nx-window-minimized"));
        Assert.That(cut.Find(".nx-window-body").HasAttribute("hidden"), Is.True);
        Assert.That(cut.Find(".nx-window-footer").HasAttribute("hidden"), Is.True);
        Assert.That(cut.Find(".nx-window-backdrop").ClassList, Does.Contain("nx-window-backdrop-hidden"));
        Assert.That(cut.Find("#w").HasAttribute("aria-modal"), Is.False);
        Assert.That(cut.Find(".nx-window-minimize").GetAttribute("aria-label"), Is.EqualTo("Restore"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "update"), Is.True);
    }

    [Test]
    public async Task Maximize_ButtonAndDoubleClick_Toggle()
    {
        var maximized = new List<bool>();
        var cut = RenderBound([], configure: p => p
            .Add(x => x.Maximizable, true)
            .Add(x => x.MaximizedChanged, (bool v) => maximized.Add(v)));

        await cut.Find(".nx-window-maximize").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("#w").ClassList, Does.Contain("nx-window-maximized"));
        Assert.That(cut.Find(".nx-window-maximize").GetAttribute("aria-label"), Is.EqualTo("Restore"));

        await cut.Find(".nx-window-titlebar").DoubleClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("#w").ClassList, Does.Not.Contain("nx-window-maximized"));
        Assert.That(maximized, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public async Task DoubleClick_WithoutMaximizable_DoesNothing()
    {
        var cut = RenderBound([]);

        await cut.Find(".nx-window-titlebar").DoubleClickAsync(new MouseEventArgs());

        Assert.That(cut.Find("#w").ClassList, Does.Not.Contain("nx-window-maximized"));
    }

    [Test]
    public void Maximized_BoundFromOutside()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Maximizable, true).Add(x => x.Maximized, true));

        Assert.That(cut.Find("#w").ClassList, Does.Contain("nx-window-maximized"));
        cut.Render(p => p.Add(x => x.Maximized, false));
        Assert.That(cut.Find("#w").ClassList, Does.Not.Contain("nx-window-maximized"));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    [Test]
    public async Task CenterAndBringToFront_CallJs()
    {
        var cut = RenderBound([]);

        await cut.InvokeAsync(cut.Instance.CenterAsync);
        await cut.InvokeAsync(cut.Instance.BringToFrontAsync);

        Assert.That(module.Invocations.Any(i => i.Identifier == "center"), Is.True);
        Assert.That(module.Invocations.Any(i => i.Identifier == "front"), Is.True);
    }

    [Test]
    public async Task Dispose_WhileOpen_ClosesInJs()
    {
        var cut = RenderBound([]);

        await cut.Instance.DisposeAsync();

        Assert.That(module.Invocations.Any(i => i.Identifier == "close"), Is.True);
    }

    [Test]
    public void AdditionalAttributes_GoToTheWindow()
    {
        var cut = RenderBound([], configure: p => p.AddUnmatched("data-test", "x").Add(x => x.Class, "mine"));

        Assert.That(cut.Find("#w").GetAttribute("data-test"), Is.EqualTo("x"));
        Assert.That(cut.Find("#w").ClassList, Does.Contain("mine"));
    }
}
