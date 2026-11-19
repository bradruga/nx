using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxPopup: placing next to the anchor, flipping and outside presses need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class PopupTests : PageTest
{
    private readonly string _baseUrl;

    public PopupTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await Page.GotoAsync($"{_baseUrl}/popup/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Opens_BelowTheAnchor_StartAligned()
    {
        await GoToPage("overview");
        await Page.Locator("#overview-anchor").ClickAsync();
        var popup = Page.Locator("#overview-popup");
        await Expect(popup).ToBeVisibleAsync();

        var a = (await Page.Locator("#overview-anchor").BoundingBoxAsync())!;
        var p = (await popup.BoundingBoxAsync())!;
        Assert.That(p.Y - (a.Y + a.Height), Is.EqualTo(4).Within(1));
        Assert.That(p.X - a.X, Is.EqualTo(0).Within(1));
        Assert.That(p.Width, Is.EqualTo(240).Within(1));
    }

    [Test]
    public async Task InsideKeeps_OutsideAndEscapeClose_AnchorToggles()
    {
        await GoToPage("overview");
        var popup = Page.Locator("#overview-popup");
        await Page.Locator("#overview-anchor").ClickAsync();
        await popup.GetByText("Stripped").ClickAsync();
        await Expect(popup).ToBeVisibleAsync();

        await Page.Mouse.ClickAsync(5, 300);
        await Expect(popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-state")).ToHaveTextAsync("Closed");

        await Page.Locator("#overview-anchor").ClickAsync();
        await Expect(popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(popup).ToHaveCountAsync(0);

        await Page.Locator("#overview-anchor").ClickAsync();
        await Expect(popup).ToBeVisibleAsync();
        await Page.Locator("#overview-anchor").ClickAsync();
        await Expect(popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task EndAligned_FilterMenu()
    {
        await GoToPage("examples");
        await Page.Locator("#mix-filter").ClickAsync();
        var popup = Page.Locator("#filter-popup");
        await Expect(popup).ToBeVisibleAsync();
        // nx-anchor.js places the popup after it renders: wait for the right edges to meet.
        await Page.WaitForFunctionAsync("""
            () => {
                const a = document.getElementById('mix-filter').getBoundingClientRect();
                const p = document.getElementById('filter-popup').getBoundingClientRect();
                return Math.abs(p.right - a.right) <= 1;
            }
            """);

        await Page.Locator("#filter-search").FillAsync("psi");
        await Expect(popup.Locator(".nx-checkbox")).ToHaveCountAsync(2);
    }

    [TestCase("bottom")]
    [TestCase("top")]
    [TestCase("right")]
    [TestCase("left")]
    public async Task Placement_EachSide(string side)
    {
        await GoToPage("examples");
        // Centered, so every side has room and the popup does not flip.
        await Page.Locator("#place-target").EvaluateAsync("e => e.scrollIntoView({ block: 'center' })");
        await Page.Locator($"#place-{side}").ClickAsync();
        var popup = Page.Locator("#place-popup");
        await Expect(popup).ToHaveAttributeAsync("data-placement", side);

        var t = (await Page.Locator("#place-target").BoundingBoxAsync())!;
        var p = (await popup.BoundingBoxAsync())!;
        var gap = side switch
        {
            "bottom" => p.Y - (t.Y + t.Height),
            "top" => t.Y - (p.Y + p.Height),
            "right" => p.X - (t.X + t.Width),
            _ => t.X - (p.X + p.Width)
        };
        Assert.That(gap, Is.EqualTo(8).Within(1));
    }

    [TestCase("#place-bottom", "#place-popup", "#place-target")]
    [TestCase("#card-anchor", "#card-popup", "#card-anchor")]
    public async Task PageScroll_PopupKeepsItsOffsetFromTheAnchor(string opener, string popupId, string anchorId)
    {
        await GoToPage("examples");
        await Page.Locator(anchorId).EvaluateAsync("a => window.scrollBy(0, a.getBoundingClientRect().top - 300)");
        await Page.Locator(opener).ClickAsync();
        var popup = Page.Locator(popupId);
        await Expect(popup).ToBeVisibleAsync();
        await Page.WaitForFunctionAsync("p => document.querySelector(p).style.top !== ''", popupId);

        const string offset = "({ p, a }) => { const pr = document.querySelector(p).getBoundingClientRect(), ar = document.querySelector(a).getBoundingClientRect(); return [pr.left - ar.left, pr.top - ar.top]; }";
        var before = await Page.EvaluateAsync<double[]>(offset, new { p = popupId, a = anchorId });
        var anchorTop = await Page.Locator(anchorId).EvaluateAsync<double>("a => a.getBoundingClientRect().top");

        await Page.EvaluateAsync("() => window.scrollBy(0, 120)");
        await Page.WaitForFunctionAsync("({ a, top }) => Math.abs(document.querySelector(a).getBoundingClientRect().top - (top - 120)) < 1",
            new { a = anchorId, top = anchorTop });
        await Page.WaitForFunctionAsync(
            "({ p, a, dx, dy }) => { const pr = document.querySelector(p).getBoundingClientRect(), ar = document.querySelector(a).getBoundingClientRect(); return Math.abs(pr.left - ar.left - dx) < 1 && Math.abs(pr.top - ar.top - dy) < 1; }",
            new { p = popupId, a = anchorId, dx = before[0], dy = before[1] });
        await Expect(popup).ToBeVisibleAsync();
    }

    [Test]
    public async Task NearTheRightEdge_ShiftsLeftToStayOnScreen()
    {
        await Page.SetViewportSizeAsync(390, 800);
        await Page.GotoAsync($"{_baseUrl}/popup/examples");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        var job = Page.Locator("#job-55117");
        await job.ScrollIntoViewIfNeededAsync();
        var viewport = await Page.EvaluateAsync<double>("() => document.documentElement.clientWidth");
        var j = (await job.BoundingBoxAsync())!;
        Assert.That(j.X + 280, Is.GreaterThan(viewport - 8), "start-aligned, the card would overflow the right edge");

        await job.HoverAsync();
        var card = Page.Locator("#hover-card");
        await Expect(card).ToContainTextAsync("55117 · Riverside Garage");
        // Right edge 8 px inside the viewport, still under its job.
        await Page.WaitForFunctionAsync("w => Math.abs(document.querySelector('#hover-card').getBoundingClientRect().right - (w - 8)) < 1.5", viewport);
        var c = (await card.BoundingBoxAsync())!;
        Assert.That((double)c.X, Is.GreaterThanOrEqualTo(8));
        Assert.That((double)c.Width, Is.EqualTo(280).Within(1));
        Assert.That((double)c.Y, Is.EqualTo((double)(j.Y + j.Height + 4)).Within(2));
    }

    [Test]
    public async Task EndAligned_AnchorNearTheLeftEdge_ShiftsRightToStayOnScreen()
    {
        await Page.SetViewportSizeAsync(390, 800);
        await Page.GotoAsync($"{_baseUrl}/popup/examples");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        var a = (await Page.Locator("#mix-filter").BoundingBoxAsync())!;
        Assert.That(a.X + a.Width - 240, Is.LessThan(8), "end-aligned, the menu would overflow the left edge");

        await Page.Locator("#mix-filter").ClickAsync();
        await Expect(Page.Locator("#filter-popup")).ToBeVisibleAsync();
        await Page.WaitForFunctionAsync("() => Math.abs(document.querySelector('#filter-popup').getBoundingClientRect().left - 8) < 1");
        var p = (await Page.Locator("#filter-popup").BoundingBoxAsync())!;
        Assert.That((double)p.Width, Is.EqualTo(240).Within(1));
    }

    [Test]
    public async Task NoRoomBelow_FlipsAbove_AndScrolls()
    {
        await GoToPage("playground");
        await Page.Locator("#pg-lines").FillAsync("20");
        await Page.Locator("#pg-lines").PressAsync("Tab");
        await Page.Locator("#playground-anchor").EvaluateAsync("a => window.scrollBy(0, a.getBoundingClientRect().top - (innerHeight - 140))");
        await Page.Locator("#playground-anchor").ClickAsync();

        var popup = Page.Locator("#playground-popup");
        await Expect(popup).ToHaveAttributeAsync("data-placement", "top");
        Assert.That(await popup.EvaluateAsync<bool>("p => p.scrollHeight > p.clientHeight"), Is.True);
        var p = (await popup.BoundingBoxAsync())!;
        Assert.That(p.Y, Is.GreaterThanOrEqualTo(8));
    }

    [Test]
    public async Task HoverCard_OpensOnRest_StaysWhileOnIt_LinkWorks_ClosesOnLeave()
    {
        await GoToPage("examples");
        var card = Page.Locator("#hover-card");
        var job = Page.Locator("#job-55117");
        await job.ScrollIntoViewIfNeededAsync();

        // A pass over a job is not a rest.
        await job.HoverAsync();
        await Page.Locator("h2", new() { HasText = "Hover Card" }).HoverAsync();
        await Page.WaitForTimeoutAsync(600);
        await Expect(card).ToHaveCountAsync(0);

        await job.HoverAsync();
        await Expect(card).ToBeVisibleAsync();
        await Expect(card).ToContainTextAsync("55117 · Riverside Garage");
        var j = (await job.BoundingBoxAsync())!;
        var c = (await card.BoundingBoxAsync())!;
        Assert.That(c.Y, Is.EqualTo(j.Y + j.Height + 4).Within(2), "under its own job");

        // Onto the card, across the gap, and it stays; the link is a real link.
        await Page.Locator("#hover-link").HoverAsync();
        await Page.WaitForTimeoutAsync(600);
        await Expect(card).ToBeVisibleAsync();
        await Expect(Page.Locator("#hover-link")).ToHaveAttributeAsync("href", "gantt/overview");

        // Another job: the same popup moves and shows that one.
        await Page.Locator("#job-55130").HoverAsync();
        await Expect(card).ToContainTextAsync("55130 · Mill St Culvert");

        await Page.Locator("h2", new() { HasText = "Hover Card" }).HoverAsync();
        await Expect(card).ToHaveCountAsync(0);
    }

    [Test]
    public async Task HoverCard_Keyboard_FocusOpens_DownEnters_EscapeReturns()
    {
        await GoToPage("examples");
        await Page.Locator("#job-55104").ScrollIntoViewIfNeededAsync();
        await Page.Locator(".doc-section:has(#card-anchor) .doc-example-code-toggle").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#job-55104")).ToBeFocusedAsync();
        await Expect(Page.Locator("#hover-card")).ToContainTextAsync("55104 · Route 9 Bridge");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#hover-link")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("#hover-card")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#job-55104")).ToBeFocusedAsync();
    }

    [Test]
    public async Task LoadedOnOpen_QueriesOnlyThePieceShown_LoaderThenHistory()
    {
        await GoToPage("examples");
        var card = Page.Locator("#lazy-card");
        var queries = Page.Locator("#lazy-queries");
        await Page.Locator("#piece-WP-007").ScrollIntoViewIfNeededAsync();
        await Expect(queries).ToHaveTextAsync("Queries run: 0 of 120");

        await Page.Locator("#piece-WP-007").HoverAsync();
        await Expect(card.Locator(".demo-history-loading")).ToBeVisibleAsync();
        await Expect(queries).ToHaveTextAsync("Queries run: 1 of 120");
        await Expect(card.Locator(".demo-history li")).ToHaveCountAsync(4);
        await Expect(card).ToContainTextAsync("WP-007");

        // The next piece starts its own query; the first one's content is gone.
        await Page.Locator("#piece-WP-008").HoverAsync();
        await Expect(Page.Locator("#lazy-last")).ToHaveTextAsync("Last query: WP-008");
        await Expect(queries).ToHaveTextAsync("Queries run: 2 of 120");
        await Expect(card.Locator(".demo-history li")).ToHaveCountAsync(4);
        await Expect(card).ToContainTextAsync("WP-008");
    }
}
