using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

/// <summary>NxMenu and the components that open it: NxContextMenu, NxDropDownButton, NxSplitButton.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxMenuTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-menu.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Text(string text) => b => b.AddContent(0, text);

    private static IReadOnlyList<NxMenuItem> Items() =>
    [
        new("Edit") { Id = "edit", Shortcut = "F2", Icon = Text("E") },
        new("Copy") { Id = "copy" },
        NxMenuItem.Separator(),
        new("Archive") { Id = "archive", Disabled = true },
        new("Show grid") { Id = "grid", Checked = true },
        new("Send to") { Id = "send", Items = [new("Plant") { Id = "plant" }, new("Office") { Id = "office" }] },
    ];

    // ── NxMenu ────────────────────────────────────────────────────────────────

    [Test]
    public void Closed_RendersNothing()
    {
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()));

        Assert.That(cut.Markup.Trim(), Is.Empty);
    }

    [Test]
    public async Task OpenAt_RendersTreeAndCallsJs()
    {
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Id, "m").Add(x => x.AriaLabel, "Actions"));

        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(100, 200));

        var root = cut.Find("#m > ul.nx-menu-list");
        Assert.That(root.GetAttribute("role"), Is.EqualTo("menu"));
        Assert.That(root.GetAttribute("aria-label"), Is.EqualTo("Actions"));
        Assert.That(root.HasAttribute("hidden"), Is.False);
        Assert.That(root.HasAttribute("style"), Is.False);
        var open = module.Invocations.Single(i => i.Identifier == "open");
        Assert.That(open.Arguments[0], Is.EqualTo("m"));
        Assert.That(open.Arguments[4], Is.EqualTo(true));
        Assert.That(cut.Instance.IsOpen, Is.True);
    }

    [Test]
    public async Task Items_RolesStatesAndParts()
    {
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Id, "m"));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        var top = cut.FindAll("#m > ul > li > .nx-menu-item");
        Assert.That(top.Select(i => i.QuerySelector(".nx-menu-text")!.TextContent), Is.EqualTo(new[] { "Edit", "Copy", "Archive", "Show grid", "Send to" }));
        Assert.That(top[0].GetAttribute("role"), Is.EqualTo("menuitem"));
        Assert.That(top[0].QuerySelector(".nx-menu-shortcut")!.TextContent, Is.EqualTo("F2"));
        Assert.That(top[0].QuerySelector(".nx-menu-icon")!.TextContent, Is.EqualTo("E"));
        Assert.That(top[2].GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(top[3].GetAttribute("role"), Is.EqualTo("menuitemcheckbox"));
        Assert.That(top[3].GetAttribute("aria-checked"), Is.EqualTo("true"));
        Assert.That(top[4].GetAttribute("aria-haspopup"), Is.EqualTo("menu"));
        Assert.That(top[4].GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(cut.FindAll("#m > ul > li[role=separator]"), Has.Count.EqualTo(1));

        var sub = cut.Find("#m > ul > li > ul.nx-menu-list");
        Assert.That(sub.HasAttribute("hidden"), Is.True);
        Assert.That(sub.GetAttribute("aria-label"), Is.EqualTo("Send to"));
        Assert.That(sub.QuerySelector(".nx-menu-back")!.TextContent.Trim(), Is.EqualTo("Send to"));
    }

    [Test]
    public async Task Click_RaisesAndCloses()
    {
        var picked = new List<NxMenuItemClickEventArgs>();
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e)));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0, "row-7"));

        await cut.FindAll(".nx-menu-item")[1].ClickAsync(new MouseEventArgs());

        Assert.That(picked.Single().Item.Id, Is.EqualTo("copy"));
        Assert.That(picked.Single().Context, Is.EqualTo("row-7"));
        Assert.That(cut.FindAll(".nx-menu-list"), Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "close"), Is.True);
    }

    [Test]
    public async Task Click_DisabledOrParent_DoesNothing()
    {
        var picked = new List<NxMenuItemClickEventArgs>();
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e)));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        await cut.Find(".nx-menu-item[aria-disabled=true]").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-menu-item[aria-haspopup=menu]").ClickAsync(new MouseEventArgs());

        Assert.That(picked, Is.Empty);
        Assert.That(cut.Instance.IsOpen, Is.True);
    }

    [Test]
    public async Task SubmenuItem_Picks()
    {
        var picked = new List<string?>();
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e.Item.Id)));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        await cut.FindAll("ul ul .nx-menu-item")[1].ClickAsync(new MouseEventArgs());

        Assert.That(picked, Is.EqualTo(new[] { "office" }));
    }

    [Test]
    public async Task Dismiss_FromJs_ClosesAndRaisesOpenChanged()
    {
        var states = new List<bool>();
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.OpenChanged, (bool v) => states.Add(v)));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        await cut.Instance.OnDismiss();

        Assert.That(states, Is.EqualTo(new[] { true, false }));
        cut.WaitForState(() => cut.FindAll(".nx-menu-list").Count == 0);
    }

    [Test]
    public async Task Backdrop_Dismisses()
    {
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, Items()));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        await cut.Find(".nx-menu-backdrop").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task NoIcons_NoIconColumn()
    {
        var cut = Render<NxMenu>(p => p.Add(x => x.Items, [new NxMenuItem("A"), new NxMenuItem("B")]));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        Assert.That(cut.FindAll(".nx-menu-icon"), Is.Empty);
    }

    // ── NxContextMenu ─────────────────────────────────────────────────────────

    [Test]
    public void ContextMenu_WatchesWrappedTarget()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, b => b.AddMarkupContent(0, "<div id=\"card\">Card</div>")));

        Assert.That(cut.Find(".nx-context-target > #card"), Is.Not.Null);
        var watch = module.Invocations.Single(i => i.Identifier == "watchContext");
        Assert.That(watch.Arguments[3], Is.Null);
    }

    [Test]
    public void ContextMenu_WatchesSelector_DisabledUnwatches()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#chip"));
        Assert.That(module.Invocations.Single(i => i.Identifier == "watchContext").Arguments[3], Is.EqualTo("#chip"));

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.That(module.Invocations.Any(i => i.Identifier == "unwatchContext"), Is.True);
    }

    [Test]
    public async Task ContextMenu_Request_RaisesOpeningThenOpens_WithContext()
    {
        var opening = new List<NxContextMenuOpeningEventArgs>();
        var picked = new List<string?>();
        var cut = Render<NxContextMenu>(p => p
            .Add(x => x.Items, Items())
            .Add(x => x.Target, "#t")
            .Add(x => x.OnOpening, (NxContextMenuOpeningEventArgs e) => opening.Add(e))
            .Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e.Context)));

        await cut.Instance.OnContextRequested(40, 60, "P-1043");

        Assert.That(opening.Single().X, Is.EqualTo(40));
        Assert.That(opening.Single().Context, Is.EqualTo("P-1043"));
        cut.WaitForState(() => cut.FindAll(".nx-menu-item").Count > 0);
        await cut.FindAll(".nx-menu-item")[0].ClickAsync(new MouseEventArgs());
        Assert.That(picked, Is.EqualTo(new[] { "P-1043" }));
    }

    [Test]
    public async Task ContextMenu_OpeningCancel_StaysClosed()
    {
        var cut = Render<NxContextMenu>(p => p
            .Add(x => x.Items, Items())
            .Add(x => x.Target, "#t")
            .Add(x => x.OnOpening, (NxContextMenuOpeningEventArgs e) => e.Cancel = true));

        await cut.Instance.OnContextRequested(0, 0, null);

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.FindAll(".nx-menu-list"), Is.Empty);
    }

    // ── NxDropDownButton ──────────────────────────────────────────────────────

    [Test]
    public async Task DropDownButton_TogglesMenu_AriaExpanded()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.Id, "ddb").Add(x => x.ChildContent, Text("Export")));

        var button = cut.Find("button#ddb");
        Assert.That(button.GetAttribute("aria-haspopup"), Is.EqualTo("menu"));
        Assert.That(button.GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(button.GetAttribute("aria-controls"), Is.EqualTo("ddb-menu"));
        Assert.That(cut.FindAll(".nx-dropdownbutton-arrow"), Has.Count.EqualTo(1));

        await button.ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("button#ddb").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(cut.Find("#ddb-menu .nx-menu-list"), Is.Not.Null);

        await cut.Find("button#ddb").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("button#ddb").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public async Task DropDownButton_ArrowDownOpens_AlignEndPassed()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.AlignEnd, true).Add(x => x.ChildContent, Text("Export")));

        await cut.Find(".nx-dropdownbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Instance.IsOpen, Is.True);
        var how = module.Invocations.Single(i => i.Identifier == "open").Arguments[3]!;
        Assert.That(how.GetType().GetProperty("alignEnd")!.GetValue(how), Is.EqualTo(true));
    }

    [Test]
    public void DropDownButton_IconOnly_NoArrow()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.Icon, Text("⋮")).Add(x => x.AriaLabel, "More"));

        Assert.That(cut.FindAll(".nx-dropdownbutton-arrow"), Is.Empty);
        Assert.That(cut.Find("button").GetAttribute("aria-label"), Is.EqualTo("More"));
    }

    [Test]
    public async Task DropDownButton_Disabled_DoesNotOpen()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.Disabled, true).Add(x => x.ChildContent, Text("Export")));

        await cut.Find(".nx-dropdownbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("button").HasAttribute("disabled"), Is.True);
    }

    // ── NxSplitButton ─────────────────────────────────────────────────────────

    [Test]
    public async Task SplitButton_MainClicks_ArrowOpens()
    {
        var clicks = 0;
        var cut = Render<NxSplitButton>(p => p
            .Add(x => x.Id, "sb")
            .Add(x => x.Items, Items())
            .Add(x => x.OnClick, (MouseEventArgs _) => clicks++)
            .Add(x => x.ChildContent, Text("Save")));

        Assert.That(cut.Find(".nx-splitbutton").GetAttribute("role"), Is.EqualTo("group"));
        await cut.Find("button#sb").ClickAsync(new MouseEventArgs());
        Assert.That(clicks, Is.EqualTo(1));
        Assert.That(cut.Instance.IsOpen, Is.False);

        var toggle = cut.Find("button#sb-toggle");
        Assert.That(toggle.GetAttribute("aria-label"), Is.EqualTo("More options"));
        Assert.That(toggle.GetAttribute("aria-haspopup"), Is.EqualTo("menu"));
        await toggle.ClickAsync(new MouseEventArgs());
        Assert.That(cut.Instance.IsOpen, Is.True);
        Assert.That(cut.Find("button#sb-toggle").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(clicks, Is.EqualTo(1));
    }

    [Test]
    public void SplitButton_MenuDisabled_OnlyArrow()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.MenuDisabled, true).Add(x => x.ChildContent, Text("Save")));

        Assert.That(cut.Find("button#sb").HasAttribute("disabled"), Is.False);
        Assert.That(cut.Find("button#sb-toggle").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task SplitButton_PickRaisesItemClick()
    {
        var picked = new List<string?>();
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Items, Items()).Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e.Item.Id)).Add(x => x.ChildContent, Text("Save")));

        await cut.InvokeAsync(cut.Instance.OpenAsync);
        await cut.FindAll(".nx-menu-item")[0].ClickAsync(new MouseEventArgs());

        Assert.That(picked, Is.EqualTo(new[] { "edit" }));
    }

    [Test]
    public async Task SplitButton_Disabled_DisablesBothHalves_DownDoesNotOpen()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.Disabled, true).Add(x => x.ChildContent, Text("Save")));

        await cut.Find(".nx-splitbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Find("button#sb").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("button#sb-toggle").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task SplitButton_Down_OpensTheMenuAlignedToTheEnd()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Save")));

        await cut.Find(".nx-splitbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Instance.IsOpen, Is.True);
        Assert.That(cut.Find("#sb-menu .nx-menu-list"), Is.Not.Null);
        var how = module.Invocations.Single(i => i.Identifier == "open").Arguments[3]!;
        Assert.That(how.GetType().GetProperty("alignEnd")!.GetValue(how), Is.EqualTo(true));
    }

    [Test]
    public async Task SplitButton_MenuDisabled_DownDoesNotOpen()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Items, Items()).Add(x => x.MenuDisabled, true).Add(x => x.ChildContent, Text("Save")));

        await cut.Find(".nx-splitbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task SplitButton_OtherKeys_DoNotOpen()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Save")));

        await cut.Find(".nx-splitbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        await cut.Find(".nx-splitbutton").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public void SplitButton_VariantAndSize_ReachBothHalves()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items())
            .Add(x => x.Variant, NxButtonVariant.Primary).Add(x => x.Size, NxButtonSize.Small).Add(x => x.ChildContent, Text("Save")));

        foreach (var id in new[] { "sb", "sb-toggle" })
            Assert.That(cut.Find($"button#{id}").ClassList, Does.Contain("nx-button-primary").And.Contain("nx-button-sm"), id);
    }

    [Test]
    public async Task SplitButton_MenuAriaLabel_NamesTheArrowAndTheMenu()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.MenuAriaLabel, "Save options").Add(x => x.ChildContent, Text("Save")));

        await cut.InvokeAsync(cut.Instance.OpenAsync);

        Assert.That(cut.Find("button#sb-toggle").GetAttribute("aria-label"), Is.EqualTo("Save options"));
        Assert.That(cut.Find("button#sb-toggle").GetAttribute("aria-controls"), Is.EqualTo("sb-menu"));
        Assert.That(cut.Find("#sb-menu > ul").GetAttribute("aria-label"), Is.EqualTo("Save options"));
    }

    [Test]
    public void SplitButton_AriaLabel_NamesTheGroupAndTheMainAction()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.AriaLabel, "Save").Add(x => x.Icon, Text("S")));

        Assert.That(cut.Find(".nx-splitbutton").GetAttribute("aria-label"), Is.EqualTo("Save"));
        Assert.That(cut.Find("button#sb").GetAttribute("aria-label"), Is.EqualTo("Save"));
    }

    [Test]
    public async Task SplitButton_ArrowClickWhileOpen_Closes()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Save")));

        await cut.Find("button#sb-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find("button#sb-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("button#sb-toggle").GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(cut.FindAll(".nx-menu-list"), Is.Empty);
    }

    [Test]
    public async Task SplitButton_Pick_ClosesTheMenu()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Save")));
        await cut.Find("button#sb-toggle").ClickAsync(new MouseEventArgs());

        await cut.FindAll(".nx-menu-item")[1].ClickAsync(new MouseEventArgs());

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("button#sb-toggle").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public void SplitButton_Loading_OnlyOnTheMainAction()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Id, "sb").Add(x => x.Items, Items()).Add(x => x.Loading, true).Add(x => x.ChildContent, Text("Save")));

        Assert.That(cut.Find("button#sb").GetAttribute("aria-busy"), Is.EqualTo("true"));
        Assert.That(cut.Find("button#sb-toggle").HasAttribute("aria-busy"), Is.False);
        Assert.That(cut.Find("button#sb-toggle").HasAttribute("disabled"), Is.False);
    }

    [Test]
    public void SplitButton_StopsDownFromScrollingThePage()
    {
        var input = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        input.Mode = JSRuntimeMode.Loose;

        Render<NxSplitButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Save")));

        var prevent = input.Invocations.Single(i => i.Identifier == "preventKeys");
        Assert.That(prevent.Arguments[1], Is.EqualTo(new[] { "ArrowDown" }));
    }

    [Test]
    public void SplitButton_ClassAndStyle_OnTheOuterElement()
    {
        var cut = Render<NxSplitButton>(p => p.Add(x => x.Items, Items()).Add(x => x.Class, "mine").Add(x => x.Style, "margin: 4px").Add(x => x.ChildContent, Text("Save")));

        var outer = cut.Find(".nx-splitbutton");
        Assert.That(outer.ClassList, Does.Contain("mine"));
        Assert.That(outer.GetAttribute("style"), Is.EqualTo("margin: 4px"));
    }

    // ── NxDropDownButton, more ────────────────────────────────────────────────

    [Test]
    public async Task DropDownButton_UpOpens()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Export")));

        await cut.Find(".nx-dropdownbutton").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.That(cut.Instance.IsOpen, Is.True);
    }

    [Test]
    public async Task DropDownButton_OtherKeys_DoNotOpen()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Export")));

        await cut.Find(".nx-dropdownbutton").KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task DropDownButton_OpensStartAlignedByDefault()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Export")));

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        var how = module.Invocations.Single(i => i.Identifier == "open").Arguments[3]!;
        Assert.That(how.GetType().GetProperty("alignEnd")!.GetValue(how), Is.EqualTo(false));
    }

    [Test]
    public void DropDownButton_ShowArrowFalse_NoArrow()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ShowArrow, false).Add(x => x.ChildContent, Text("Export")));

        Assert.That(cut.FindAll(".nx-dropdownbutton-arrow"), Is.Empty);
        Assert.That(cut.Find("button").TextContent.Trim(), Is.EqualTo("Export"));
    }

    [Test]
    public void DropDownButton_VariantSizeAndFullWidth_ReachTheButton()
    {
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Items, Items())
            .Add(x => x.Variant, NxButtonVariant.Danger).Add(x => x.Size, NxButtonSize.Large).Add(x => x.FullWidth, true).Add(x => x.ChildContent, Text("Export")));

        Assert.That(cut.Find("button").ClassList, Does.Contain("nx-button-danger").And.Contain("nx-button-lg").And.Contain("nx-button-full-width"));
    }

    [Test]
    public async Task DropDownButton_MenuIsNamedByAriaLabel_UnlessMenuAriaLabel()
    {
        var plain = Render<NxDropDownButton>(p => p.Add(x => x.Id, "a").Add(x => x.Items, Items()).Add(x => x.AriaLabel, "Export"));
        await plain.InvokeAsync(plain.Instance.OpenAsync);
        Assert.That(plain.Find("#a-menu > ul").GetAttribute("aria-label"), Is.EqualTo("Export"));

        var named = Render<NxDropDownButton>(p => p.Add(x => x.Id, "b").Add(x => x.Items, Items()).Add(x => x.AriaLabel, "Export").Add(x => x.MenuAriaLabel, "Formats"));
        await named.InvokeAsync(named.Instance.OpenAsync);
        Assert.That(named.Find("#b-menu > ul").GetAttribute("aria-label"), Is.EqualTo("Formats"));
    }

    [Test]
    public async Task DropDownButton_Pick_RaisesItemClick_AndCloses()
    {
        var picked = new List<string?>();
        var cut = Render<NxDropDownButton>(p => p.Add(x => x.Id, "ddb").Add(x => x.Items, Items())
            .Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e.Item.Id)).Add(x => x.ChildContent, Text("Export")));
        await cut.Find("button#ddb").ClickAsync(new MouseEventArgs());

        await cut.FindAll(".nx-menu-item")[1].ClickAsync(new MouseEventArgs());

        Assert.That(picked, Is.EqualTo(new[] { "copy" }));
        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("button#ddb").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public void DropDownButton_StopsUpAndDownFromScrollingThePage()
    {
        var input = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        input.Mode = JSRuntimeMode.Loose;

        Render<NxDropDownButton>(p => p.Add(x => x.Items, Items()).Add(x => x.ChildContent, Text("Export")));

        var prevent = input.Invocations.Single(i => i.Identifier == "preventKeys");
        Assert.That(prevent.Arguments[1], Is.EqualTo(new[] { "ArrowDown", "ArrowUp" }));
    }

    // ── NxContextMenu, more ───────────────────────────────────────────────────

    [Test]
    public async Task ContextMenu_OpenAtFromCode_OpensAtThePoint_WithoutOpening_ContextReachesThePick()
    {
        var opening = 0;
        var picked = new List<string?>();
        var cut = Render<NxContextMenu>(p => p
            .Add(x => x.Items, Items())
            .Add(x => x.Target, "#t")
            .Add(x => x.OnOpening, (NxContextMenuOpeningEventArgs _) => opening++)
            .Add(x => x.OnItemClick, (NxMenuItemClickEventArgs e) => picked.Add(e.Context)));

        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(12, 34, "row-3"));

        Assert.That(cut.Instance.IsOpen, Is.True);
        Assert.That(opening, Is.Zero);
        var how = module.Invocations.Single(i => i.Identifier == "open").Arguments[3]!;
        Assert.That((how.GetType().GetProperty("x")!.GetValue(how), how.GetType().GetProperty("y")!.GetValue(how)), Is.EqualTo((12d, 34d)));
        await cut.FindAll(".nx-menu-item")[0].ClickAsync(new MouseEventArgs());
        Assert.That(picked, Is.EqualTo(new[] { "row-3" }));
    }

    [Test]
    public async Task ContextMenu_CloseAsync_Closes()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#t"));
        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        await cut.InvokeAsync(cut.Instance.CloseAsync);

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.FindAll(".nx-menu-list"), Is.Empty);
    }

    [Test]
    public async Task ContextMenu_Opening_ChangesTheItemsForThePressedRow()
    {
        var host = Render<ContextHost>();
        var menu = host.FindComponent<NxContextMenu>();

        await menu.Instance.OnContextRequested(0, 0, "locked");

        host.WaitForState(() => host.FindAll(".nx-menu-item").Count > 0);
        Assert.That(host.FindAll(".nx-menu-text").Select(t => t.TextContent), Is.EqualTo(new[] { "Unlock" }));
    }

    [Test]
    public void ContextMenu_ReEnabled_WatchesAgain()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#chip").Add(x => x.Disabled, true));
        Assert.That(module.Invocations.Count(i => i.Identifier == "watchContext"), Is.Zero);

        cut.Render(p => p.Add(x => x.Disabled, false));

        Assert.That(module.Invocations.Single(i => i.Identifier == "watchContext").Arguments[3], Is.EqualTo("#chip"));
    }

    [Test]
    public void ContextMenu_NewTarget_WatchesTheNewSelector_UnderTheSameKey()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#one"));

        cut.Render(p => p.Add(x => x.Target, "#two"));

        var watches = module.Invocations.Where(i => i.Identifier == "watchContext").ToList();
        Assert.That(watches.Select(w => w.Arguments[3]), Is.EqualTo(new[] { "#one", "#two" }));
        Assert.That(watches[1].Arguments[0], Is.EqualTo(watches[0].Arguments[0]));
    }

    [Test]
    public void ContextMenu_RerenderWithSameTarget_DoesNotWatchAgain()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#one"));

        cut.Render(p => p.Add(x => x.AriaLabel, "Row"));

        Assert.That(module.Invocations.Count(i => i.Identifier == "watchContext"), Is.EqualTo(1));
    }

    [Test]
    public async Task ContextMenu_IdClassAndAriaLabel_ReachTheMenu()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#t").Add(x => x.Id, "cm").Add(x => x.Class, "mine").Add(x => x.AriaLabel, "Row actions"));

        await cut.InvokeAsync(() => cut.Instance.OpenAtAsync(0, 0));

        Assert.That(cut.Find("#cm").ClassList, Does.Contain("nx-context-menu").And.Contain("mine"));
        Assert.That(cut.Find("#cm > ul").GetAttribute("aria-label"), Is.EqualTo("Row actions"));
    }

    [Test]
    public async Task ContextMenu_Dispose_Unwatches()
    {
        var cut = Render<NxContextMenu>(p => p.Add(x => x.Items, Items()).Add(x => x.Target, "#t"));
        var key = module.Invocations.Single(i => i.Identifier == "watchContext").Arguments[0];

        await cut.Instance.DisposeAsync();

        Assert.That(module.Invocations.Single(i => i.Identifier == "unwatchContext").Arguments[0], Is.EqualTo(key));
    }

    /// <summary>A page whose OnOpening picks the items for the pressed row, as the demo's rows do.</summary>
    private sealed class ContextHost : ComponentBase
    {
        private IReadOnlyList<NxMenuItem> items = [new("Lock")];

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b)
        {
            b.OpenComponent<NxContextMenu>(0);
            b.AddComponentParameter(1, nameof(NxContextMenu.Items), items);
            b.AddComponentParameter(2, nameof(NxContextMenu.Target), "#rows");
            b.AddComponentParameter(3, nameof(NxContextMenu.OnOpening), EventCallback.Factory.Create<NxContextMenuOpeningEventArgs>(this,
                e => items = e.Context == "locked" ? [new("Unlock")] : [new("Lock")]));
            b.CloseComponent();
        }
    }
}
