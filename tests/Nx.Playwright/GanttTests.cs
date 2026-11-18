using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxGantt: synced scrolling, the divider, editing in place and drawing links need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class GanttTests : PageTest
{
    private readonly string _baseUrl;

    public GanttTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page, int width = 1280)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync($"{_baseUrl}/gantt/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Gantt(string id) => Page.Locator($"#{id}");

    [Test]
    public async Task CellEdit_EndsOnAPressOutside_RaisesOnUpdate()
    {
        await GoToPage("overview");
        var g = Gantt("overview-gantt");
        await g.Locator("[role=row]").Filter(new() { HasText = "Shop drawings" }).Locator("[role=gridcell]").First.ClickAsync();
        await g.Locator(".nx-gantt-td-editing input").FillAsync("Shop drawings rev 2");
        await Expect(g.Locator(".nx-gantt-td-editing")).ToHaveCountAsync(1);

        await Page.Locator("h2", new() { HasText = "Basic Usage" }).ClickAsync();
        await Expect(g.Locator(".nx-gantt-td-editing")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-updated")).ToHaveTextAsync("Updated: Shop drawings rev 2");
        await Expect(g.Locator(".nx-gantt-bar").Filter(new() { HasText = "rev 2" })).ToHaveCountAsync(1);
    }

    private async Task DragAsync(ILocator from, float dx, float dy = 0)
    {
        var box = (await from.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width / 2 + dx, box.Y + box.Height / 2 + dy, new() { Steps = 8 });
    }

    [Test]
    public async Task Bar_DragMoves_GripResizes_SnappedToDays()
    {
        await GoToPage("examples");
        var g = Gantt("edit-gantt");
        var bar = g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Forms set" });
        await bar.ScrollIntoViewIfNeededAsync();
        var before = (await bar.BoundingBoxAsync())!;

        // 2.4 days to the right lands on two days (a day is 30 px).
        await DragAsync(bar, 72);
        await Expect(g.Locator(".nx-gantt-bar-ghost")).ToHaveCountAsync(1);
        var ghost = (await g.Locator(".nx-gantt-bar-ghost").BoundingBoxAsync())!;
        Assert.That(ghost.X - before.X, Is.EqualTo(60).Within(1), "the outline snaps to whole days");
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("Move: Forms set");
        await Expect(g.Locator(".nx-gantt-bar-ghost")).ToHaveCountAsync(0);
        Assert.That((await bar.BoundingBoxAsync())!.X - before.X, Is.EqualTo(60).Within(1));
        Assert.That((await bar.BoundingBoxAsync())!.Width, Is.EqualTo(before.Width).Within(1));

        // The right edge out a day: three days become four.
        await bar.HoverAsync();
        await DragAsync(bar.Locator(".nx-gantt-grip-end"), 30);
        await Page.Mouse.UpAsync();
        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("ResizeEnd: Forms set");
        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("(4 d)");
        Assert.That((await bar.BoundingBoxAsync())!.Width - before.Width, Is.EqualTo(30).Within(1));
    }

    [Test]
    public async Task Bar_EscapeDropsTheDrag_ClickStillSelects_SummaryStaysPut()
    {
        await GoToPage("examples");
        var g = Gantt("edit-gantt");
        var bar = g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Forms set" });
        await bar.ScrollIntoViewIfNeededAsync();
        var before = (await bar.BoundingBoxAsync())!;

        await DragAsync(bar, 90);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(g.Locator(".nx-gantt-bar-ghost")).ToHaveCountAsync(0);
        await Page.Mouse.UpAsync();
        await Expect(Page.Locator("#edit-log")).ToHaveTextAsync("Drag a bar, or one of its ends");
        Assert.That((await bar.BoundingBoxAsync())!.X, Is.EqualTo(before.X).Within(1));

        await bar.ClickAsync();
        await Expect(Page.Locator("#edit-selected")).ToHaveTextAsync("Selected: Forms set");

        var summary = g.Locator(".nx-gantt-bar-summary").First;
        await Expect(summary).Not.ToHaveAttributeAsync("data-nx-gantt-drag", "true");
    }

    [Test]
    public async Task Editable_Off_NothingDrags_AddAndDelete()
    {
        await GoToPage("examples");
        var g = Gantt("edit-gantt");
        var rows = g.Locator("[role=row]");
        var count = await rows.CountAsync();

        await g.Locator("[role=row]").Filter(new() { HasText = "Forms set" }).Locator("[role=gridcell]").Nth(1).ClickAsync();
        await Page.Locator("#edit-add").ClickAsync();
        await Expect(rows).ToHaveCountAsync(count + 1);
        await Expect(Page.Locator("#edit-selected")).ToHaveTextAsync("Selected: New task");

        await g.Locator("[role=row][aria-selected=true]").FocusAsync();
        await Page.Keyboard.PressAsync("Delete");
        await Expect(rows).ToHaveCountAsync(count);
        await Expect(Page.Locator("#edit-log")).ToHaveTextAsync("Deleted: New task");

        await Page.Locator("#edit-editable").ClickAsync();
        await Expect(Page.Locator("#edit-editable")).ToHaveAttributeAsync("aria-checked", "false");
        await Expect(g.Locator("[data-nx-gantt-drag]")).ToHaveCountAsync(0);
        await Expect(g.Locator(".nx-gantt-handle")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#edit-add")).ToBeDisabledAsync();
    }

    [Test]
    public async Task DoubleClick_OpensThePageForm()
    {
        await GoToPage("overview");
        await Gantt("overview-gantt").Locator(".nx-gantt-bar").Filter(new() { HasText = "Forms set" }).DblClickAsync();
        await Expect(Page.Locator("#task-title")).ToHaveValueAsync("Forms set");
        await Page.Locator("#task-title").FillAsync("Forms set (steel)");
        await Page.Locator("#task-save").ClickAsync();
        await Expect(Gantt("overview-gantt").Locator("[role=row]").Filter(new() { HasText = "Forms set (steel)" })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Divider_Drag_ReportsTheWidth()
    {
        await GoToPage("overview");
        var d = (await Gantt("overview-gantt").Locator(".nx-gantt-divider").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(d.X + 3, d.Y + 100);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(d.X - 100, d.Y + 100, new() { Steps = 5 });
        await Page.Mouse.UpAsync();
        await Expect(Page.Locator("#overview-width")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"Task list: \d+px"));
    }

    [Test]
    public async Task Panes_ScrollTogether()
    {
        await GoToPage("playground");
        await Page.Locator("#pg-height").SelectOptionAsync("360px");
        var g = Gantt("playground-gantt");
        await g.Locator(".nx-gantt-time-body").EvaluateAsync("e => e.scrollTop = 80");
        await Page.WaitForFunctionAsync("() => document.querySelector('#playground-gantt .nx-gantt-tree-body').scrollTop > 0");
        Assert.That(await g.Locator(".nx-gantt-tree-body").EvaluateAsync<double>("e => e.scrollTop"),
            Is.EqualTo(await g.Locator(".nx-gantt-time-body").EvaluateAsync<double>("e => e.scrollTop")).Within(1));

        await g.Locator(".nx-gantt-time-body").EvaluateAsync("e => e.scrollLeft = 200");
        await Page.WaitForFunctionAsync("() => document.querySelector('#playground-gantt .nx-gantt-time-head').scrollLeft === 200");
    }

    [Test]
    public async Task DrawLink_EndToStart()
    {
        await GoToPage("examples");
        var g = Gantt("links-gantt");
        var a = g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Shop drawings" });
        await a.HoverAsync();
        var h = (await a.Locator("[data-nx-gantt-handle=end]").BoundingBoxAsync())!;
        var b = (await g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Engineer review" }).BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(h.X + h.Width / 2, h.Y + h.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(b.X + 10, b.Y + b.Height / 2, new() { Steps = 8 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator("#links-count")).ToContainTextAsync("Shop drawings → Engineer review (FinishStart)");
        await Expect(g.Locator(".nx-gantt-links > path")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task SortAndFilter_ShowTheClearButtons()
    {
        await GoToPage("examples");
        var g = Gantt("filter-gantt");
        await g.Locator(".nx-gantt-sort").Filter(new() { HasText = "Task" }).ClickAsync();
        await Expect(Page.Locator("#clear-sort")).ToBeVisibleAsync();
        await g.Locator("input[aria-label='Filter Resources']").FillAsync("crew b");
        await Expect(g.Locator("[role=row]")).ToHaveCountAsync(3);
        await Page.Locator("#clear-filter").ClickAsync();
        await Expect(g.Locator("[role=row]")).ToHaveCountAsync(10);
    }

    [Test]
    public async Task Hover_ShowsTheTooltip()
    {
        await GoToPage("overview");
        var g = Gantt("overview-gantt");
        await g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Production" }).HoverAsync();
        await Expect(g.Locator(".nx-gantt-tooltip")).ToContainTextAsync("Production");
    }
}
