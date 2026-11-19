using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>
/// The demo site's section tools (samples/Nx.Demo/wwwroot/demo-sections.js): heading links, the "On this page" list
/// and jump control, and the Phone / Tablet / Desktop frames of an example.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DemoSectionsTests : PageTest
{
    private readonly string _baseUrl;

    public DemoSectionsTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Toc     => Page.Locator(".ds-toc");
    private ILocator Jump    => Page.Locator(".ds-jump select");
    private ILocator Dialog  => Page.Locator("dialog.ds-frame-dialog");
    private IFrameLocator Frame => Page.FrameLocator(".ds-frame-iframe");

    private async Task GoToAsync(string path, int width = 1440, int height = 900)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    /// <summary>Waits until the heading's top sits just under the sticky top bar.</summary>
    private Task WaitForHeadingAtTopAsync(string id) =>
        Page.WaitForFunctionAsync(
            "id => { const h = document.querySelector(`h2[data-section-id='${id}']`); if (!h) return false; const t = h.getBoundingClientRect().top; return t > 50 && t < 140; }",
            id, new() { Timeout = 5000 });

    private async Task OpenFrameAsync(string sectionId, string device)
    {
        await Page.Locator($".ds-h2-tools[data-id={sectionId}] a.ds-frame-link[data-device={device}]").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Frame.Locator(".layout-container[data-interactive=true]")).ToBeAttachedAsync();
    }

    // ── Heading ids and links ─────────────────────────────────────────────────

    [Test]
    public async Task Headings_GetSectionIdsByTheSharedRule_NotIdAttributes()
    {
        await GoToAsync("/combobox/examples");

        var mismatches = await Page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('.doc-section > h2')]
                .filter(h => h.dataset.sectionId !== h.textContent.toLowerCase().replace(/&/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, ''))
                .map(h => h.textContent + ' -> ' + h.dataset.sectionId)
            """);
        Assert.That(mismatches, Is.Empty);
        // Components in the examples use the same ids (#hover-card, #search-box): headings must not take them.
        await Expect(Page.Locator(".doc-section > h2[id]")).ToHaveCountAsync(0);
        await Expect(Page.Locator("h2[data-section-id=value-selected-item]")).ToHaveTextAsync("Value & Selected Item");
    }

    [Test]
    public async Task HashOfASectionWhoseIdAComponentUses_LandsOnTheHeading()
    {
        await GoToAsync("/autocomplete/examples#search-box");

        await Expect(Page.Locator("#search-box")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#search-box")).ToHaveJSPropertyAsync("tagName", "INPUT");
        await Page.WaitForFunctionAsync("""
            () => { const h = [...document.querySelectorAll('.doc-section > h2')].find(h => h.textContent.trim() === 'Search Box');
                    const t = h?.getBoundingClientRect().top; return t > 50 && t < 140; }
            """, null, new() { Timeout = 5000 });
    }

    [Test]
    public async Task HashLink_CopiesTheSectionAddress()
    {
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await GoToAsync("/combobox/examples");

        var heading = Page.Locator("h2[data-section-id=filtering]");
        await heading.HoverAsync();
        await Page.Locator(".ds-h2-tools[data-id=filtering] a.ds-anchor").ClickAsync();

        await Expect(Page.Locator(".ds-toast")).ToHaveTextAsync("Link copied");
        await Expect(Page).ToHaveURLAsync(new Regex("/combobox/examples#filtering$"));
        var copied = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.That(copied, Is.EqualTo(_baseUrl + "/combobox/examples#filtering"));
    }

    [Test]
    public async Task LoadingWithAHash_LandsOnThatSection()
    {
        await GoToAsync("/combobox/examples#multi-column");

        await WaitForHeadingAtTopAsync("multi-column");
        await Expect(Toc.Locator(".ds-toc-active")).ToHaveTextAsync("Multi-Column");
    }

    // ── On this page ──────────────────────────────────────────────────────────

    [Test]
    public async Task Desktop_ListShowsEverySection_AndHighlightsWhileScrolling()
    {
        await GoToAsync("/combobox/examples");

        await Expect(Toc).ToBeVisibleAsync();
        await Expect(Jump).ToBeHiddenAsync();
        var headings = await Page.Locator(".doc-section > h2").AllTextContentsAsync();
        await Expect(Toc.Locator(".ds-toc-link")).ToHaveTextAsync(headings.Select(h => h.Trim()).ToArray());
        await Expect(Toc.Locator(".ds-toc-active")).ToHaveTextAsync("Simple List");

        await Page.Locator("h2[data-section-id=grouping]").EvaluateAsync("h => h.scrollIntoView({ block: 'start' })");
        await Expect(Toc.Locator(".ds-toc-active")).ToHaveTextAsync("Grouping");
        await Expect(Toc.Locator(".ds-toc-active")).ToHaveAttributeAsync("aria-current", "location");

        // The list sits beside the content, not over it.
        var list = (await Toc.BoundingBoxAsync())!;
        var section = (await Page.Locator(".doc-section").First.BoundingBoxAsync())!;
        Assert.That(list.X, Is.GreaterThanOrEqualTo(section.X + section.Width));
    }

    [Test]
    public async Task Desktop_ClickingAnEntry_ScrollsToItAndSetsTheHash()
    {
        await GoToAsync("/combobox/examples");

        await Toc.GetByRole(AriaRole.Link, new() { Name = "Multi-Select", Exact = true }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/combobox/examples#multi-select$"));
        await WaitForHeadingAtTopAsync("multi-select");
        await Expect(Toc.Locator(".ds-toc-active")).ToHaveTextAsync("Multi-Select");
    }

    [TestCase(1280, 900)]
    [TestCase(768, 1024)]
    public async Task Narrower_JumpControlSitsInTheTabRow(int width, int height)
    {
        await GoToAsync("/combobox/examples", width, height);

        await Expect(Toc).ToBeHiddenAsync();
        await Expect(Page.Locator(".doc-tabs .ds-jump select")).ToBeVisibleAsync();
        var tabs = (await Page.Locator(".doc-tabs").BoundingBoxAsync())!;
        var tab = (await Page.Locator(".doc-tab").First.BoundingBoxAsync())!;
        Assert.That(tabs.Height, Is.LessThanOrEqualTo(tab.Height + 2), "the jump control made the tab row taller");

        await Jump.SelectOptionAsync("custom-values");
        await Expect(Page).ToHaveURLAsync(new Regex("#custom-values$"));
        await WaitForHeadingAtTopAsync("custom-values");
    }

    [Test]
    public async Task PagesWithFewSections_HaveNoList()
    {
        await GoToAsync("/button/playground");

        var count = await Page.Locator(".doc-section > h2").CountAsync();
        Assume.That(count, Is.LessThan(3), "test setup: the playground has three or more sections");
        await Expect(Toc).ToHaveCountAsync(0);
        await Expect(Page.Locator(".ds-jump")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Phone_JumpControlScrollsToTheSection_NoSidewaysScroll_44pxTargets()
    {
        await GoToAsync("/combobox/examples", 390, 844);

        await Expect(Toc).ToBeHiddenAsync();
        await Expect(Jump).ToBeVisibleAsync();
        Assert.That((await Jump.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));

        // Under the page header.
        var tabs = (await Page.Locator(".doc-tabs").BoundingBoxAsync())!;
        Assert.That((await Jump.BoundingBoxAsync())!.Y, Is.GreaterThan(tabs.Y + tabs.Height - 1));

        foreach (var target in await Page.Locator("a.ds-anchor, a.ds-frame-link").AllAsync())
        {
            var box = (await target.BoundingBoxAsync())!;
            Assert.That(box.Height, Is.GreaterThanOrEqualTo(44));
            Assert.That(box.Width, Is.GreaterThanOrEqualTo(44));
        }
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"),
            Is.LessThanOrEqualTo(0));

        await Jump.SelectOptionAsync("grouping");
        await Expect(Page).ToHaveURLAsync(new Regex("#grouping$"));
        await WaitForHeadingAtTopAsync("grouping");
    }

    // ── Frames ────────────────────────────────────────────────────────────────

    [Test]
    public async Task PhoneFrame_ShowsOnlyThatExample_At390_AndItsPhoneSheet()
    {
        await GoToAsync("/combobox/examples");

        await OpenFrameAsync("mobile-overlay", "phone");

        await Expect(Dialog.Locator(".ds-frame-title")).ToHaveTextAsync("Mobile Overlay");
        await Expect(Dialog.Locator(".ds-frame-device[aria-pressed=true]")).ToHaveTextAsync("Phone");
        await Expect(Dialog.Locator(".ds-frame-size")).ToContainTextAsync("390");
        var frameWidth = await Frame.Locator("html").EvaluateAsync<int>("() => window.innerWidth");
        Assert.That(frameWidth, Is.EqualTo(390));

        // Bare: no site chrome, no other section, no tools.
        await Expect(Frame.Locator(".top-nav")).ToBeHiddenAsync();
        await Expect(Frame.Locator(".nav-sidebar")).ToBeHiddenAsync();
        await Expect(Frame.Locator("h1")).ToBeHiddenAsync();
        await Expect(Frame.Locator(".doc-section:visible")).ToHaveCountAsync(1);
        await Expect(Frame.Locator("#mobile-combo")).ToBeVisibleAsync();
        await Expect(Frame.Locator(".ds-toc, .ds-jump")).ToHaveCountAsync(0);

        await Frame.Locator("#mobile-combo").ClickAsync();
        var overlay = Frame.Locator(".nx-combobox-overlay");
        await Expect(overlay).ToBeVisibleAsync();
        Assert.That(await overlay.EvaluateAsync<int>("el => Math.round(el.getBoundingClientRect().width)"), Is.EqualTo(390));

        await Dialog.Locator(".ds-frame-close").ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    [Test]
    public async Task PhoneFrame_MediaQueriesSeeTheFrameWidth()
    {
        await GoToAsync("/combobox/examples");

        // The third field has MobileMode Auto: the overlay only below 600 px of viewport.
        await OpenFrameAsync("mobile-overlay", "phone");
        await Frame.Locator("#mobile-auto").ClickAsync();
        await Expect(Frame.Locator(".nx-combobox-overlay")).ToBeVisibleAsync();

        // The same frame at desktop width opens the ordinary popup.
        await Frame.Locator(".nx-combobox-overlay").GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Expect(Frame.Locator(".nx-combobox-overlay")).ToBeHiddenAsync();
        await Dialog.Locator(".ds-frame-device[data-device=desktop]").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('.ds-frame-iframe').contentWindow.innerWidth === 1280");
        await Frame.Locator("#mobile-auto").ClickAsync();
        await Frame.Locator("#mobile-auto").PressAsync("ArrowDown");
        await Expect(Frame.Locator(".nx-combobox-popup")).ToBeVisibleAsync();
        await Expect(Dialog.Locator(".ds-frame-size")).ToContainTextAsync("shown at");
    }

    [Test]
    public async Task Frame_FollowsTheSiteTheme()
    {
        await GoToAsync("/combobox/examples");
        await Page.Locator(".top-nav-theme").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveClassAsync(new Regex("nx-theme-dark"));

        await OpenFrameAsync("filtering", "tablet");
        await Expect(Frame.Locator("html")).ToHaveClassAsync(new Regex("nx-theme-dark"));

        await Page.EvaluateAsync("() => window.__nxSetDark(false)");
        await Expect(Frame.Locator("html")).Not.ToHaveClassAsync(new Regex("nx-theme-dark"));
        await Page.EvaluateAsync("() => localStorage.removeItem('nx-demo-theme')");
    }

    [Test]
    public async Task FrameButtons_OnlyOnExamplesPages()
    {
        await GoToAsync("/combobox/api");

        await Expect(Page.Locator("a.ds-anchor").First).ToBeAttachedAsync();
        await Expect(Page.Locator("a.ds-frame-link")).ToHaveCountAsync(0);
    }
}
