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
        // The bar redraws a render after the log line: wait for it to land.
        await bar.EvaluateAsync("(b, x) => new Promise(r => { const f = () => Math.abs(b.getBoundingClientRect().left - x) <= 1 ? r() : requestAnimationFrame(f); f(); })", (double)before.X + 60);
        Assert.That((await bar.BoundingBoxAsync())!.Width, Is.EqualTo(before.Width).Within(1));

        // The right edge out a day: three days become four.
        await bar.HoverAsync();
        await DragAsync(bar.Locator(".nx-gantt-grip-end"), 30);
        await Page.Mouse.UpAsync();
        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("ResizeEnd: Forms set");
        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("(4 d)");
        await bar.EvaluateAsync("(b, w) => new Promise(r => { const f = () => Math.abs(b.getBoundingClientRect().width - w) <= 1 ? r() : requestAnimationFrame(f); f(); })", (double)before.Width + 30);
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
    public async Task CellEdit_ClickAnotherRow_ItsEditorOpensFocused()
    {
        await GoToPage("examples");
        var g = Gantt("edit-gantt");
        var editor = g.Locator(".nx-gantt-td-editing input");
        await g.Locator("[role=row]").Filter(new() { HasText = "Forms set" }).Locator("[role=gridcell]").First.ClickAsync();
        await Expect(editor).ToHaveValueAsync("Forms set");

        await g.Locator("[role=row]").Filter(new() { HasText = "Cure and strip" }).Locator("[role=gridcell]").First.ClickAsync();
        await Expect(g.Locator(".nx-gantt-td-editing")).ToHaveCountAsync(1);
        await Expect(editor).ToHaveValueAsync("Cure and strip");
        await Expect(editor).ToBeVisibleAsync();
        await Expect(editor).ToBeFocusedAsync();
    }

    /// <summary>A bar's left and width inside the timeline canvas, so a scrolled timeline does not move it.</summary>
    private Task<double[]> BarInCanvasAsync(string gantt, string title) => Page.EvaluateAsync<double[]>("""
        ([id, title]) => {
            const g = document.getElementById(id);
            const c = g.querySelector('.nx-gantt-canvas').getBoundingClientRect();
            const b = [...g.querySelectorAll('.nx-gantt-bar')].find(b => b.textContent.trim() === title).getBoundingClientRect();
            return [b.left - c.left, b.width];
        }
        """, new[] { gantt, title });

    [Test]
    public async Task AltArrows_MoveTheBar_ThenShrinkItsEnd_NoPageScroll()
    {
        await GoToPage("examples");
        var g = Gantt("edit-gantt");
        await g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Forms set" }).ScrollIntoViewIfNeededAsync();
        var before = await BarInCanvasAsync("edit-gantt", "Forms set");
        var scrollX = await Page.EvaluateAsync<double>("() => window.scrollX");

        await g.Locator("[role=row]").Filter(new() { HasText = "Forms set" }).FocusAsync();
        await Expect(Page.Locator("#edit-selected")).ToHaveTextAsync("Selected: Forms set");
        await Page.Keyboard.PressAsync("Alt+ArrowRight");
        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("Move: Forms set");
        var moved = await BarInCanvasAsync("edit-gantt", "Forms set");
        Assert.That(moved[0] - before[0], Is.EqualTo(30).Within(1), "one slot (a day is 30 px)");
        Assert.That(moved[1], Is.EqualTo(before[1]).Within(1));

        await Page.Keyboard.PressAsync("Alt+Shift+ArrowLeft");
        await Expect(Page.Locator("#edit-log")).ToContainTextAsync("ResizeEnd: Forms set");
        var shrunk = await BarInCanvasAsync("edit-gantt", "Forms set");
        Assert.That(shrunk[0], Is.EqualTo(moved[0]).Within(1));
        Assert.That(before[1] - shrunk[1], Is.EqualTo(30).Within(1));

        Assert.That(Page.Url, Does.EndWith("/gantt/examples"), "Alt+Left is not the browser's Back");
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollX"), Is.EqualTo(scrollX));
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }

    /// <summary>Each arrow's end and its successor bar's left, in page x; "Shop drawings → Engineer review" is the first link.</summary>
    private Task<double[][]> ArrowEndsAsync() => Page.EvaluateAsync<double[][]>("""
        () => {
            const g = document.getElementById('playground-gantt');
            const svg = g.querySelector('.nx-gantt-links');
            const left = svg.getBoundingClientRect().left;
            const bar = t => [...g.querySelectorAll('.nx-gantt-bar')].find(b => b.textContent.trim() === t).getBoundingClientRect().left;
            const ends = [...svg.querySelectorAll(':scope > path')].map(p => left + p.getPointAtLength(p.getTotalLength()).x);
            return [[ends[0], bar('Engineer review')], [ends[1], bar('Forms set')]];
        }
        """);

    [Test]
    public async Task Zoom_ScalesBarsAndArrows_ArrowsEndAtTheirSuccessor()
    {
        await GoToPage("playground");
        var g = Gantt("playground-gantt");
        var bar = g.Locator(".nx-gantt-bar").Filter(new() { HasText = "Forms set" });
        var before = (await bar.BoundingBoxAsync())!;
        Assert.That(before.Width, Is.EqualTo(90).Within(1), "three days at 30 px");
        var canvas = (await g.Locator(".nx-gantt-canvas").BoundingBoxAsync())!;
        var arrowsBefore = await ArrowEndsAsync();

        await Page.Locator("#pg-slot").FillAsync("60");
        await Page.Locator("#pg-slot").PressAsync("Tab");
        await Page.WaitForFunctionAsync("() => [...document.querySelectorAll('#playground-gantt .nx-gantt-bar')].find(b => b.textContent.trim() === 'Forms set').getBoundingClientRect().width > 170");

        var after = (await bar.BoundingBoxAsync())!;
        var canvasAfter = (await g.Locator(".nx-gantt-canvas").BoundingBoxAsync())!;
        Assert.That(after.Width, Is.EqualTo(180).Within(1));
        Assert.That(after.X - canvasAfter.X, Is.EqualTo(2 * (before.X - canvas.X)).Within(1), "the bar's offset scales with the slot");
        var arrows = await ArrowEndsAsync();
        for (var i = 0; i < arrows.Length; i++)
        {
            Assert.That(arrows[i][0], Is.EqualTo(arrows[i][1]).Within(2), $"arrow {i} ends at its successor's left");
            Assert.That(arrows[i][0] - canvasAfter.X, Is.EqualTo(2 * (arrowsBefore[i][0] - canvas.X)).Within(2), $"arrow {i} scales");
        }
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
