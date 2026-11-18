using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxSplitterTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-splitter.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private sealed class PaneSpec
    {
        public string Id = "";
        public string? Size;
        public string? Min, Max, Title;
        public bool Collapsed, Resizable = true;
        public List<string?> SizeLog = [];
        public List<bool> CollapseLog = [];
    }

    private IRenderedComponent<NxSplitter> RenderSplitter(PaneSpec[] panes, NxOrientation orientation = NxOrientation.Horizontal)
    {
        RenderFragment content = b =>
        {
            foreach (var pane in panes)
            {
                b.OpenComponent<NxSplitterPane>(0);
                b.AddAttribute(1, nameof(NxSplitterPane.Id), pane.Id);
                b.AddAttribute(2, nameof(NxSplitterPane.Size), pane.Size);
                b.AddAttribute(3, nameof(NxSplitterPane.SizeChanged), EventCallback.Factory.Create<string?>(this, v => { pane.Size = v; pane.SizeLog.Add(v); }));
                b.AddAttribute(4, nameof(NxSplitterPane.Min), pane.Min);
                b.AddAttribute(5, nameof(NxSplitterPane.Max), pane.Max);
                b.AddAttribute(6, nameof(NxSplitterPane.Title), pane.Title);
                b.AddAttribute(8, nameof(NxSplitterPane.Collapsed), pane.Collapsed);
                b.AddAttribute(9, nameof(NxSplitterPane.CollapsedChanged), EventCallback.Factory.Create<bool>(this, v => { pane.Collapsed = v; pane.CollapseLog.Add(v); }));
                b.AddAttribute(10, nameof(NxSplitterPane.Resizable), pane.Resizable);
                b.AddAttribute(11, nameof(NxSplitterPane.ChildContent), (RenderFragment)(c => c.AddContent(0, $"Content {pane.Id}")));
                b.CloseComponent();
            }
        };
        var cut = Render<NxSplitter>(p => p.Add(x => x.Id, "s").Add(x => x.Orientation, orientation).Add(x => x.ChildContent, content));
        cut.WaitForState(() => cut.FindAll(".nx-splitter-bar").Count == panes.Length - 1);
        return cut;
    }

    private static PaneSpec Pane(string id, string? size = null, string? title = null) => new() { Id = id, Size = size, Title = title ?? id.ToUpperInvariant() };

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_PanesWithBarsBetween()
    {
        var cut = RenderSplitter([Pane("a", "30%"), Pane("b"), Pane("c", "200px")]);

        var root = cut.Find("#s");
        Assert.That(root.ClassList, Does.Contain("nx-splitter-horizontal"));
        Assert.That(root.GetAttribute("data-orientation"), Is.EqualTo("horizontal"));
        var children = root.Children.Select(c => c.ClassList.Contains("nx-splitter-bar") ? "bar" : c.Id).ToList();
        Assert.That(children, Is.EqualTo(new[] { "a", "bar", "b", "bar", "c" }));
    }

    [Test]
    public void PaneStyle_FromSizeMinMax()
    {
        var a = Pane("a", "30%");
        a.Min = "120px";
        a.Max = "70%";
        var cut = RenderSplitter([a, Pane("b")]);

        Assert.That(cut.Find("#a").GetAttribute("style"), Is.EqualTo("flex: 0 0 30%; min-width: 120px; max-width: 70%"));
        Assert.That(cut.Find("#b").GetAttribute("style"), Is.EqualTo("flex: 1 1 0px"));
        Assert.That(cut.Find("#b").ClassList, Does.Contain("nx-splitter-pane-flexible"));
        Assert.That(cut.Find("#a").GetAttribute("data-unit"), Is.EqualTo("%"));
        Assert.That(cut.Find("#a").GetAttribute("data-min"), Is.EqualTo("120px"));
    }

    [Test]
    public void Vertical_UsesHeightLimits_AndHorizontalSeparators()
    {
        var a = Pane("a", "40%");
        a.Min = "60px";
        var cut = RenderSplitter([a, Pane("b")], NxOrientation.Vertical);

        Assert.That(cut.Find("#a").GetAttribute("style"), Does.Contain("min-height: 60px"));
        Assert.That(cut.Find(".nx-splitter-bar").GetAttribute("aria-orientation"), Is.EqualTo("horizontal"));
        Assert.That(cut.Find("#s").ClassList, Does.Contain("nx-splitter-vertical"));
    }

    [Test]
    public void Bar_IsALabelledFocusableSeparator()
    {
        var cut = RenderSplitter([Pane("a", "30%", "Jobs"), Pane("b")]);

        var bar = cut.Find(".nx-splitter-bar");
        Assert.That(bar.GetAttribute("role"), Is.EqualTo("separator"));
        Assert.That(bar.GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(bar.GetAttribute("aria-orientation"), Is.EqualTo("vertical"));
        Assert.That(bar.GetAttribute("aria-label"), Is.EqualTo("Resize Jobs"));
        Assert.That(bar.GetAttribute("aria-controls"), Is.EqualTo("a"));
        Assert.That(bar.GetAttribute("aria-valuemin"), Is.EqualTo("0"));
        Assert.That(bar.GetAttribute("aria-valuemax"), Is.EqualTo("100"));
    }

    [Test]
    public void NotResizable_BarIsFixed()
    {
        var a = Pane("a", "160px");
        a.Resizable = false;
        var cut = RenderSplitter([a, Pane("b")]);

        var bar = cut.Find(".nx-splitter-bar");
        Assert.That(bar.ClassList, Does.Contain("nx-splitter-bar-fixed"));
        Assert.That(bar.GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(bar.GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(cut.Find("#a").GetAttribute("data-resizable"), Is.EqualTo("false"));
    }

    [Test]
    public void Title_RendersPhoneHeader()
    {
        var cut = RenderSplitter([Pane("a", "30%", "Jobs"), Pane("b")]);

        Assert.That(cut.Find("#a .nx-splitter-pane-header .nx-splitter-pane-title").TextContent, Is.EqualTo("Jobs"));
        Assert.That(cut.Find("#a .nx-splitter-pane-header").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void Js_InitCalled()
    {
        RenderSplitter([Pane("a", "30%"), Pane("b")]);

        Assert.That(module.Invocations.Any(i => i.Identifier == "init"), Is.True);
        Assert.That(module.Invocations.Any(i => i.Identifier == "refresh"), Is.True);
    }

    // ── Resizing ──────────────────────────────────────────────────────────────

    [Test]
    public async Task JsResize_SetsSizeAndRaisesChanged()
    {
        var a = Pane("a", "30%");
        var cut = RenderSplitter([a, Pane("b")]);

        await cut.Instance.OnPaneResized(0, "42.5%");

        Assert.That(a.SizeLog, Is.EqualTo(new[] { "42.5%" }));
        cut.WaitForState(() => cut.Find("#a").GetAttribute("style")!.StartsWith("flex: 0 0 42.5%"));
    }

    [Test]
    public async Task JsResize_OutOfRangeIndex_Ignored()
    {
        var cut = RenderSplitter([Pane("a", "30%"), Pane("b")]);

        await cut.Instance.OnPaneResized(7, "10px");

        Assert.That(cut.Find("#a").GetAttribute("style"), Does.StartWith("flex: 0 0 30%"));
    }

    // ── Collapsing ────────────────────────────────────────────────────────────

    [Test]
    public void NoCollapseControls_OnTheBarOrThePhoneHeader()
    {
        var cut = RenderSplitter([Pane("a", "200px", "Navigation"), Pane("b"), Pane("c", "180px", "Properties")]);

        Assert.That(cut.FindAll(".nx-splitter-bar button, .nx-splitter-pane-header button, button.nx-splitter-pane-header"), Is.Empty);
        Assert.That(cut.Find("#a .nx-splitter-pane-header").TextContent.Trim(), Is.EqualTo("Navigation"));
    }

    [Test]
    public async Task ToggleAsync_HidesContentKeepsIt_DisablesBar_RaisesChanged()
    {
        var a = Pane("a", "200px", "Navigation");
        var cut = RenderSplitter([a, Pane("b")]);
        var pane = cut.FindComponents<NxSplitterPane>()[0].Instance;

        await cut.InvokeAsync(pane.ToggleAsync);

        Assert.That(a.CollapseLog, Is.EqualTo(new[] { true }));
        cut.WaitForState(() => cut.Find("#a").ClassList.Contains("nx-splitter-pane-collapsed"));
        Assert.That(cut.Find("#a").GetAttribute("style"), Is.EqualTo("flex: 0 0 0px"));
        Assert.That(cut.Find("#a .nx-splitter-pane-content").HasAttribute("hidden"), Is.True);
        Assert.That(cut.Find("#a .nx-splitter-pane-content").TextContent, Is.EqualTo("Content a"));
        cut.WaitForState(() => cut.Find(".nx-splitter-bar").GetAttribute("aria-disabled") == "true");
        Assert.That(cut.Find(".nx-splitter-bar").ClassList, Does.Contain("nx-splitter-bar-hidden"), "nothing to resize: the bar goes with the pane");

        await cut.InvokeAsync(pane.ToggleAsync);
        Assert.That(a.CollapseLog, Is.EqualTo(new[] { true, false }));
        cut.WaitForState(() => !cut.Find("#a").ClassList.Contains("nx-splitter-pane-collapsed"));
        cut.WaitForState(() => !cut.Find(".nx-splitter-bar").ClassList.Contains("nx-splitter-bar-hidden"));
    }

    [Test]
    public void Collapsed_BoundFromOutside()
    {
        var a = Pane("a", "200px");
        a.Collapsed = true;
        var cut = RenderSplitter([a, Pane("b")]);

        Assert.That(cut.Find("#a").ClassList, Does.Contain("nx-splitter-pane-collapsed"));
    }

    [Test]
    public void PaneOutsideSplitter_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxSplitterPane>());
    }
}
