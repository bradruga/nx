using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxPopupTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-anchor.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxPopup> RenderPopup(bool visible, Action<ComponentParameterCollectionBuilder<NxPopup>>? configure = null) =>
        Render<NxPopup>(p =>
        {
            p.Add(x => x.Id, "pop");
            p.Add(x => x.Visible, visible);
            p.Add(x => x.AnchorSelector, "#anchor");
            p.AddChildContent("<span id=\"inside\">Hi</span>");
            configure?.Invoke(p);
        });

    [Test]
    public void Hidden_RendersNothing_NoJs()
    {
        var cut = RenderPopup(false);
        Assert.That(cut.Markup.Trim(), Is.Empty);
        Assert.That(module.Invocations, Is.Empty);
    }

    [Test]
    public void Visible_RendersDialog_OpensWithOptions()
    {
        var cut = RenderPopup(true, p => p
            .Add(x => x.Placement, NxPopupPlacement.Top)
            .Add(x => x.Align, NxPopupAlign.End)
            .Add(x => x.Width, "260px")
            .Add(x => x.AriaLabel, "Filter")
            .Add(x => x.Class, "mine"));

        var popup = cut.Find("#pop");
        Assert.That(popup.GetAttribute("role"), Is.EqualTo("dialog"));
        Assert.That(popup.GetAttribute("aria-label"), Is.EqualTo("Filter"));
        Assert.That(popup.ClassList, Does.Contain("nx-popup").And.Contain("mine"));
        Assert.That(popup.HasAttribute("style"), Is.False);
        Assert.That(cut.Find("#inside").TextContent, Is.EqualTo("Hi"));

        var open = module.Invocations.Single(i => i.Identifier == "open");
        Assert.That(open.Arguments[0], Is.EqualTo("pop"));
        var options = open.Arguments[3]!.ToString();
        Assert.That(options, Does.Contain("anchor = #anchor").And.Contain("placement = top").And.Contain("align = end").And.Contain("width = 260px"));
    }

    [Test]
    public void Rerender_Updates_Close_Closes()
    {
        var cut = RenderPopup(true);
        cut.Render(p => p.Add(x => x.Offset, 10));
        Assert.That(module.Invocations.Count(i => i.Identifier == "update"), Is.EqualTo(1));

        cut.Render(p => p.Add(x => x.Visible, false));
        Assert.That(cut.FindAll("#pop"), Is.Empty);
        Assert.That(module.Invocations.Single(i => i.Identifier == "close").Arguments[0], Is.EqualTo("pop"));
    }

    [Test]
    public async Task Dismiss_ClosesAndRaisesVisibleChanged()
    {
        var raised = new List<bool>();
        var cut = RenderPopup(true, p => p.Add(x => x.VisibleChanged, (bool v) => raised.Add(v)));

        await cut.InvokeAsync(cut.Instance.OnDismiss);

        Assert.That(raised, Is.EqualTo(new[] { false }));
        Assert.That(cut.FindAll("#pop"), Is.Empty);
    }

    [Test]
    public async Task RefreshAsync_PlacesAgainWhileOpen()
    {
        var cut = RenderPopup(true);
        await cut.InvokeAsync(cut.Instance.RefreshAsync);
        Assert.That(module.Invocations.Single(i => i.Identifier == "place").Arguments[0], Is.EqualTo("pop"));
    }

    [Test]
    public void Role_CanBeChanged()
    {
        var cut = RenderPopup(true, p => p.Add(x => x.Role, "menu"));
        Assert.That(cut.Find("#pop").GetAttribute("role"), Is.EqualTo("menu"));
    }

    // ── Hover card ────────────────────────────────────────────────────────────

    [Test]
    public void OpenOnHover_WatchesTheAnchorsWhileClosed()
    {
        var cut = Render<NxPopup>(p => p.Add(x => x.Id, "pop").Add(x => x.OpenOnHover, true).Add(x => x.AnchorSelector, ".job").Add(x => x.ShowDelay, 300));

        Assert.That(cut.FindAll(".nx-popup"), Is.Empty);
        var watch = module.Invocations.Single(i => i.Identifier == "watchHover");
        Assert.That(watch.Arguments[0], Is.EqualTo("pop"));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(watch.Arguments[2]), Is.EqualTo("{\"anchor\":\".job\",\"showDelay\":300,\"hideDelay\":250}"));

        cut.Render(p => p.Add(x => x.OpenOnHover, false));
        Assert.That(module.Invocations.Count(i => i.Identifier == "unwatchHover"), Is.EqualTo(1));
    }

    [Test]
    public async Task HoverOpen_RaisesOnOpeningWithTheContext_ThenOpens_DismissCloses()
    {
        var contexts = new List<string?>();
        var visible = new List<bool>();
        var cut = RenderPopup(false, p => p
            .Add(x => x.OpenOnHover, true)
            .Add(x => x.OnOpening, (NxPopupOpeningEventArgs e) => contexts.Add(e.Context))
            .Add(x => x.VisibleChanged, (bool v) => visible.Add(v)));

        var opened = await cut.InvokeAsync(() => cut.Instance.OnHoverOpen("55104"));

        Assert.That(opened, Is.True);
        Assert.That(contexts, Is.EqualTo(new[] { "55104" }));
        Assert.That(visible, Is.EqualTo(new[] { true }));
        Assert.That(cut.FindAll(".nx-popup"), Has.Count.EqualTo(1));

        // Another anchor while open: the content changes, nothing reopens.
        await cut.InvokeAsync(() => cut.Instance.OnHoverOpen("55117"));
        Assert.That(contexts, Is.EqualTo(new[] { "55104", "55117" }));
        Assert.That(visible, Is.EqualTo(new[] { true }));

        await cut.InvokeAsync(cut.Instance.OnDismiss);
        Assert.That(visible, Is.EqualTo(new[] { true, false }));
        Assert.That(cut.FindAll(".nx-popup"), Is.Empty);
    }

    [Test]
    public async Task HoverOpen_Cancelled_StaysClosed()
    {
        var cut = RenderPopup(false, p => p.Add(x => x.OpenOnHover, true).Add(x => x.OnOpening, (NxPopupOpeningEventArgs e) => e.Cancel = true));

        var opened = await cut.InvokeAsync(() => cut.Instance.OnHoverOpen(null));

        Assert.That(opened, Is.False);
        Assert.That(cut.FindAll(".nx-popup"), Is.Empty);
    }

    [Test]
    public async Task HoverOpen_PageRendersWhileOpening_ContentStartsOnce()
    {
        // As @bind-Visible does, with content that makes the page render again as it starts (a query counter).
        var visible = false;
        var starts = 0;
        IRenderedComponent<NxPopup>? cut = null;
        void Rerender() => cut!.Render(p => p.Add(x => x.Visible, visible));
        cut = Render<NxPopup>(p => p
            .Add(x => x.Id, "pop")
            .Add(x => x.AnchorSelector, ".job")
            .Add(x => x.OpenOnHover, true)
            .Add(x => x.Visible, visible)
            .Add(x => x.VisibleChanged, (bool v) => { visible = v; Rerender(); })
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<Starter>(0);
                b.AddAttribute(1, nameof(Starter.OnStart), EventCallback.Factory.Create(this, () => { starts++; Rerender(); }));
                b.CloseComponent();
            }));

        await cut.InvokeAsync(() => cut.Instance.OnHoverOpen("55104"));

        Assert.That(cut.FindAll(".nx-popup"), Has.Count.EqualTo(1));
        Assert.That(starts, Is.EqualTo(1));
    }

    [Test]
    public void Options_CarryTheCloseRulesOffsetMaxHeightAndHover()
    {
        RenderPopup(true, p => p
            .Add(x => x.CloseOnOutsideClick, false)
            .Add(x => x.CloseOnEscape, false)
            .Add(x => x.Offset, 12)
            .Add(x => x.MaxHeight, "300px")
            .Add(x => x.OpenOnHover, true));

        var options = module.Invocations.Single(i => i.Identifier == "open").Arguments[3]!.ToString();
        Assert.That(options, Does.Contain("offset = 12").And.Contain("maxHeight = 300px")
            .And.Contain("closeOnOutside = False").And.Contain("closeOnEscape = False").And.Contain("hover = True"));
    }

    [Test]
    public void Options_Defaults_BottomStart_ClosesOnOutsideAndEscape()
    {
        RenderPopup(true);

        var options = module.Invocations.Single(i => i.Identifier == "open").Arguments[3]!.ToString();
        Assert.That(options, Does.Contain("placement = bottom").And.Contain("align = start").And.Contain("offset = 4")
            .And.Contain("closeOnOutside = True").And.Contain("closeOnEscape = True").And.Contain("hover = False"));
    }

    [Test]
    public void OpenFromOutside_OpensInJsOnce_ThenUpdates()
    {
        var cut = RenderPopup(false);

        cut.Render(p => p.Add(x => x.Visible, true));
        cut.Render(p => p.Add(x => x.Width, "200px"));

        Assert.That(module.Invocations.Count(i => i.Identifier == "open"), Is.EqualTo(1));
        Assert.That(module.Invocations.Count(i => i.Identifier == "update"), Is.EqualTo(1));
    }

    [Test]
    public void AdditionalAttributes_GoToThePopup()
    {
        var cut = RenderPopup(true, p => p.AddUnmatched("data-test", "card"));
        Assert.That(cut.Find("#pop").GetAttribute("data-test"), Is.EqualTo("card"));
    }

    [Test]
    public async Task Dismiss_WhileHidden_RaisesNothing()
    {
        var raised = new List<bool>();
        var cut = RenderPopup(false, p => p.Add(x => x.VisibleChanged, (bool v) => raised.Add(v)));

        await cut.InvokeAsync(cut.Instance.OnDismiss);

        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task RefreshAsync_WhileClosed_DoesNothing()
    {
        var cut = RenderPopup(false);
        await cut.InvokeAsync(cut.Instance.RefreshAsync);
        Assert.That(module.Invocations.Any(i => i.Identifier == "place"), Is.False);
    }

    [Test]
    public void OpenOnHover_NewDelaysOrSelector_WatchAgain_OtherChangesDoNot()
    {
        var cut = Render<NxPopup>(p => p.Add(x => x.Id, "pop").Add(x => x.AnchorSelector, ".job").Add(x => x.OpenOnHover, true));

        cut.Render(p => p.Add(x => x.Width, "300px"));
        Assert.That(module.Invocations.Count(i => i.Identifier == "watchHover"), Is.EqualTo(1));

        cut.Render(p => p.Add(x => x.HideDelay, 600));
        cut.Render(p => p.Add(x => x.AnchorSelector, ".pour"));

        var watches = module.Invocations.Where(i => i.Identifier == "watchHover").Select(i => System.Text.Json.JsonSerializer.Serialize(i.Arguments[2])).ToList();
        Assert.That(watches, Is.EqualTo(new[]
        {
            "{\"anchor\":\".job\",\"showDelay\":400,\"hideDelay\":250}",
            "{\"anchor\":\".job\",\"showDelay\":400,\"hideDelay\":600}",
            "{\"anchor\":\".pour\",\"showDelay\":400,\"hideDelay\":600}"
        }));
    }

    [Test]
    public void NotOpenOnHover_NeverWatches()
    {
        RenderPopup(false);
        RenderPopup(true);
        Assert.That(module.Invocations.Any(i => i.Identifier is "watchHover" or "unwatchHover"), Is.False);
    }

    [Test]
    public async Task HoverOpen_AlreadyVisible_DoesNotRaiseVisibleChanged()
    {
        var visible = new List<bool>();
        var contexts = new List<string?>();
        var cut = RenderPopup(true, p => p.Add(x => x.OpenOnHover, true)
            .Add(x => x.OnOpening, (NxPopupOpeningEventArgs e) => contexts.Add(e.Context))
            .Add(x => x.VisibleChanged, (bool v) => visible.Add(v)));

        var opened = await cut.InvokeAsync(() => cut.Instance.OnHoverOpen("7"));

        Assert.That(opened, Is.True);
        Assert.That(contexts, Is.EqualTo(new[] { "7" }));
        Assert.That(visible, Is.Empty);
    }

    [Test]
    public async Task Dispose_UnwatchesAndCloses()
    {
        RenderPopup(true, p => p.Add(x => x.OpenOnHover, true));

        await DisposeComponentsAsync();

        Assert.That(module.Invocations.Single(i => i.Identifier == "unwatchHover").Arguments[0], Is.EqualTo("pop"));
        Assert.That(module.Invocations.Single(i => i.Identifier == "close").Arguments[0], Is.EqualTo("pop"));
    }

    private sealed class Starter : ComponentBase
    {
        [Parameter] public EventCallback OnStart { get; set; }

        protected override Task OnInitializedAsync() => OnStart.InvokeAsync();
    }
}
