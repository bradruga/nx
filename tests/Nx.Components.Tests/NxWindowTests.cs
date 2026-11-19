using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

    private static string? Option(object options, string name) => options.GetType().GetProperty(name)!.GetValue(options)?.ToString();

    [Test]
    public void Open_PassesDragHeightAndMinimums_TrapsFocusWhenModal()
    {
        RenderBound([], configure: p => p.Add(x => x.Draggable, false).Add(x => x.Height, "500px").Add(x => x.MinWidth, 320).Add(x => x.MinHeight, 200));

        var options = module.Invocations.Single(i => i.Identifier == "open").Arguments[4]!;
        Assert.That(Option(options, "draggable"), Is.EqualTo("False"));
        Assert.That(Option(options, "height"), Is.EqualTo("500px"));
        Assert.That(Option(options, "minWidth"), Is.EqualTo("320"));
        Assert.That(Option(options, "minHeight"), Is.EqualTo("200"));
        Assert.That(Option(options, "trapFocus"), Is.EqualTo("True"));
    }

    [Test]
    public void NonModal_DoesNotTrapFocus_NoBackdropToJs()
    {
        RenderBound([], configure: p => p.Add(x => x.Modal, false));

        var open = module.Invocations.Single(i => i.Identifier == "open");
        Assert.That(open.Arguments[2], Is.Null);
        Assert.That(Option(open.Arguments[4]!, "trapFocus"), Is.EqualTo("False"));
    }

    [Test]
    public async Task Minimized_ReleasesTheFocusTrapAndModality_InJs()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Minimizable, true));

        await cut.Find(".nx-window-minimize").ClickAsync(new MouseEventArgs());

        var update = module.Invocations.Last(i => i.Identifier == "update");
        Assert.That(update.Arguments[0], Is.EqualTo("w"));
        Assert.That(Option(update.Arguments[1]!, "modal"), Is.EqualTo("False"));
        Assert.That(Option(update.Arguments[1]!, "trapFocus"), Is.EqualTo("False"));
    }

    [Test]
    public void ModalChangedWhileOpen_RebindsInJs()
    {
        var cut = RenderBound([]);

        cut.Render(p => p.Add(x => x.Modal, false));

        var rebind = module.Invocations.Single(i => i.Identifier == "rebind");
        Assert.That(rebind.Arguments[0], Is.EqualTo("w"));
        Assert.That(rebind.Arguments[2], Is.Null);
        Assert.That(cut.FindAll(".nx-window-backdrop"), Is.Empty);
    }

    [Test]
    public void RerenderWithSameOptions_SendsNoUpdate()
    {
        var cut = RenderBound([]);

        cut.Render(p => p.Add(x => x.ChildContent, Text("Other body")));

        Assert.That(module.Invocations.Any(i => i.Identifier == "update"), Is.False);
    }

    // ── Moved and resized ─────────────────────────────────────────────────────

    [Test]
    public async Task MovedAndResized_FromJs_RaiseTheEvents()
    {
        NxWindowMovedEventArgs? moved = null;
        NxWindowResizedEventArgs? resized = null;
        var cut = RenderBound([], configure: p => p
            .Add(x => x.OnMoved, (NxWindowMovedEventArgs e) => moved = e)
            .Add(x => x.OnResized, (NxWindowResizedEventArgs e) => resized = e));

        await cut.InvokeAsync(() => cut.Instance.NotifyMoved(120, 80));
        await cut.InvokeAsync(() => cut.Instance.NotifyResized(640, 360));

        Assert.That(moved, Is.EqualTo(new NxWindowMovedEventArgs(120, 80)));
        Assert.That(resized, Is.EqualTo(new NxWindowResizedEventArgs(640, 360)));
    }

    // ── NxWindowHost ──────────────────────────────────────────────────────────

    /// <summary>A layout (#layout, with the host) around a page (#page) that declares two windows, one inside the other's body.</summary>
    private sealed class Page : ComponentBase
    {
        public bool Host = true, Declared = true, Open = true, Inline, InnerOpen, SecondOpen;
        public string Body = "Body";
        public readonly List<bool> Raised = [];

        public void Set(Action<Page> change)
        {
            change(this);
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", "page");
            builder.OpenComponent<CascadingValue<string>>(2);
            builder.AddComponentParameter(3, "Value", "from the page");
            builder.AddComponentParameter(4, "ChildContent", (RenderFragment)(b =>
            {
                if (Declared)
                {
                    b.OpenComponent<NxWindow>(0);
                    b.AddComponentParameter(1, "Id", "w");
                    b.AddComponentParameter(2, "Title", "Edit pour");
                    b.AddComponentParameter(3, "Visible", Open);
                    b.AddComponentParameter(4, "Inline", Inline);
                    b.AddComponentParameter(5, "VisibleChanged", EventCallback.Factory.Create<bool>(this, v => { Raised.Add(v); Open = v; }));
                    b.AddComponentParameter(6, "ChildContent", (RenderFragment)(c =>
                    {
                        c.AddContent(0, Body);
                        c.OpenComponent<Probe>(1);
                        c.CloseComponent();
                        c.OpenComponent<NxWindow>(2);
                        c.AddComponentParameter(3, "Id", "inner");
                        c.AddComponentParameter(4, "Visible", InnerOpen);
                        c.AddComponentParameter(5, "ChildContent", Text("Inner"));
                        c.CloseComponent();
                    }));
                    b.CloseComponent();
                }
                b.OpenComponent<NxWindow>(10);
                b.AddComponentParameter(11, "Id", "second");
                b.AddComponentParameter(12, "Visible", SecondOpen);
                b.AddComponentParameter(13, "ChildContent", Text("Second"));
                b.CloseComponent();
            }));
            builder.CloseComponent();
            builder.CloseElement();
            builder.OpenElement(5, "div");
            builder.AddAttribute(6, "id", "layout");
            if (Host)
            {
                builder.OpenComponent<NxWindowHost>(7);
                builder.CloseComponent();
            }
            builder.CloseElement();
        }
    }

    /// <summary>Shows the string cascaded to where it is drawn.</summary>
    private sealed class Probe : ComponentBase
    {
        [CascadingParameter] private string? Cascaded { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddMarkupContent(0, $"<span class=\"probe\">{Cascaded ?? "nothing"}</span>");
    }

    private IRenderedComponent<Page> RenderPage(Action<Page>? setup = null)
    {
        Services.AddNxDialogs();
        var cut = Render<Page>();
        if (setup is not null)
            cut.InvokeAsync(() => cut.Instance.Set(setup));
        return cut;
    }

    private int Calls(string identifier, string id) =>
        module.Invocations.Count(i => i.Identifier == identifier && (string?)i.Arguments[0] == id);

    [Test]
    public void Host_DrawsTheWindowThere_NotInPlace()
    {
        var cut = RenderPage();

        Assert.That(cut.FindAll("#layout #w[role=dialog]"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("#layout .nx-window-backdrop"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("#page").InnerHtml.Trim(), Is.Empty);
        Assert.That(Calls("open", "w"), Is.EqualTo(1));
    }

    [Test]
    public void Host_NoneInTheLayout_DrawsInPlace()
    {
        Services.AddNxDialogs();
        var cut = Render<Page>();
        cut.InvokeAsync(() => cut.Instance.Set(p => p.Host = false));

        // The host left while the window was open: it is drawn in place again and re-opened on its new elements.
        Assert.That(cut.FindAll("#page #w"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("#layout").InnerHtml.Trim(), Is.Empty);
        Assert.That(Calls("close", "w"), Is.EqualTo(1));
        Assert.That(Calls("open", "w"), Is.EqualTo(2));
    }

    [Test]
    public void Host_PageRerender_PushesNewContent_WithoutReopening()
    {
        var cut = RenderPage();

        cut.InvokeAsync(() => cut.Instance.Set(p => p.Body = "Changed"));

        Assert.That(cut.Find("#layout .nx-window-body").TextContent, Does.StartWith("Changed"));
        Assert.That(Calls("open", "w"), Is.EqualTo(1));
        Assert.That(Calls("close", "w"), Is.EqualTo(0));
    }

    [Test]
    public async Task Host_CloseButton_RaisesVisibleChanged_ClosesInJs()
    {
        var cut = RenderPage();

        await cut.Find("#layout .nx-window-close").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Instance.Raised, Is.EqualTo(new[] { false }));
        Assert.That(cut.FindAll("#w"), Is.Empty);
        Assert.That(cut.Find("#layout").InnerHtml.Trim(), Is.Empty);
        Assert.That(Calls("close", "w"), Is.EqualTo(1));

        await cut.InvokeAsync(() => cut.Instance.Set(p => p.Open = true));
        Assert.That(cut.FindAll("#layout #w"), Has.Count.EqualTo(1));
        Assert.That(Calls("open", "w"), Is.EqualTo(2));
    }

    [Test]
    public void Host_ContentLosesThePagesCascade_InlineKeepsIt()
    {
        var cut = RenderPage();
        Assert.That(cut.Find("#layout .probe").TextContent, Is.EqualTo("nothing"));

        cut.InvokeAsync(() => cut.Instance.Set(p => p.Inline = true));

        // Inline while open: back in place, re-opened in JS on the new elements.
        Assert.That(cut.Find("#page .probe").TextContent, Is.EqualTo("from the page"));
        Assert.That(cut.FindAll("#layout #w"), Is.Empty);
        Assert.That(Calls("close", "w"), Is.EqualTo(1));
        Assert.That(Calls("open", "w"), Is.EqualTo(2));
    }

    [Test]
    public void Host_Inline_OpensInPlace()
    {
        var cut = RenderPage(p => { p.Open = false; p.Inline = true; });
        var opens = Calls("open", "w");

        cut.InvokeAsync(() => cut.Instance.Set(p => p.Open = true));

        Assert.That(cut.FindAll("#page #w"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("#layout").InnerHtml.Trim(), Is.Empty);
        Assert.That(Calls("open", "w"), Is.EqualTo(opens + 1));
    }

    [Test]
    public void Host_WindowInsideAWindow_BothInTheHost_SideBySide()
    {
        var cut = RenderPage(p => p.InnerOpen = true);

        Assert.That(cut.FindAll("#layout #inner"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("#w #inner"), Is.Empty);
        Assert.That(Calls("open", "inner"), Is.EqualTo(1));

        // The outer window closes: the inner one, declared in its body, goes with it.
        cut.InvokeAsync(() => cut.Instance.Set(p => p.Open = false));
        Assert.That(cut.Find("#layout").InnerHtml.Trim(), Is.Empty);
        Assert.That(Calls("close", "inner"), Is.EqualTo(1));
    }

    [Test]
    public void Host_ClosingOneWindow_LeavesTheOthersElementsAlone()
    {
        var cut = RenderPage(p => p.SecondOpen = true);
        Assert.That(cut.FindAll("#layout [role=dialog]").Select(w => w.Id), Is.EqualTo(new[] { "w", "second" }));
        var second = cut.FindComponents<NxWindow>().Single(w => w.Instance.Id == "second").Instance;
        var captured = typeof(NxWindow).GetField("window", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var opened = (ElementReference)module.Invocations.Single(i => i.Identifier == "open" && (string?)i.Arguments[0] == "second").Arguments[1]!;
        Assert.That(((ElementReference)captured.GetValue(second)!).Id, Is.EqualTo(opened.Id));

        cut.InvokeAsync(() => cut.Instance.Set(p => p.Open = false));

        // Keyed: "second" keeps the element nx-window.js holds, rather than taking over the closed window's.
        Assert.That(cut.FindAll("#layout [role=dialog]").Select(w => w.Id), Is.EqualTo(new[] { "second" }));
        Assert.That(((ElementReference)captured.GetValue(second)!).Id, Is.EqualTo(opened.Id));
        Assert.That(Calls("open", "second"), Is.EqualTo(1));
        Assert.That(Calls("close", "second"), Is.EqualTo(0));
    }

    [Test]
    public void Host_WindowRemovedFromThePage_LeavesTheHost()
    {
        var cut = RenderPage();

        cut.InvokeAsync(() => cut.Instance.Set(p => p.Declared = false));

        Assert.That(cut.Find("#layout").InnerHtml.Trim(), Is.Empty);
        Assert.That(Calls("close", "w"), Is.EqualTo(1));
    }

    [Test]
    public void Host_WithoutItsService_Throws()
    {
        Assert.That(() => Render<NxWindowHost>(), Throws.InvalidOperationException.With.Message.Contains("AddNxDialogs"));
    }
}
