using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>
/// The demo site on a phone, a tablet and a desktop: no page scrolls sideways, and the navigation works
/// at each size. Keep <see cref="Components"/> in step with samples/Nx.Demo/Data/DemoCatalog.cs.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SiteLayoutTests : PageTest
{
    private readonly string _baseUrl;

    public SiteLayoutTests(string baseUrl) => _baseUrl = baseUrl;

    private static readonly string[] Components = ["button", "textbox", "numeric", "combobox", "checkbox", "switch", "radio", "togglegroup",
                                                          "datepicker", "daterange", "timepicker", "window", "tabstrip", "splitter",
                                                          "dropdown", "multiselect", "textarea", "tooltip", "contextmenu", "dropdownbutton", "splitbutton",
                                                          "form", "icon", "loader", "datetimepicker", "autocomplete",
                                                          "buttongroup", "colorpicker", "progressbar", "notification",
                                                          "dialog", "mediaquery", "togglebutton", "upload", "fileselect", "treeview", "maskedtextbox"];
    private static readonly string[] SubPages = ["overview", "examples", "playground", "api"];

    private static IEnumerable<string> AllPaths() =>
        new[] { "/" }.Concat(Components.SelectMany(c => SubPages.Select(p => $"/{c}/{p}")));

    private ILocator Hamburger => Page.Locator(".doc-hamburger");
    private ILocator Sidebar   => Page.Locator("#site-nav");

    private async Task GoToAsync(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<int> HorizontalOverflowAsync() =>
        Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

    // ── No horizontal scroll ──────────────────────────────────────────────────

    [TestCase(390, 844)]
    [TestCase(768, 1024)]
    [TestCase(1280, 900)]
    public async Task EveryPage_HasNoHorizontalOverflow(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        var failures = new List<string>();

        foreach (var path in AllPaths())
        {
            await GoToAsync(path);
            var overflow = await HorizontalOverflowAsync();
            if (overflow > 0)
                failures.Add($"{path}: {overflow}px");
        }

        Assert.That(failures, Is.Empty, $"pages wider than a {width}px viewport");
    }

    [TestCase(390, 844)]
    [TestCase(768, 1024)]
    public async Task OpenPlaygroundsAndPopups_DoNotWidenThePage(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await GoToAsync("/combobox/examples");

        await Page.Locator("#columns-combo").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator(".nx-combobox-popup, .nx-combobox-overlay").First).ToBeVisibleAsync();

        Assert.That(await HorizontalOverflowAsync(), Is.LessThanOrEqualTo(0));
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [TestCase(390, 844)]
    [TestCase(768, 1024)]
    public async Task Narrow_DrawerOpensFromHamburger_AndClosesOnNavigation(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await GoToAsync("/");

        await Expect(Hamburger).ToBeVisibleAsync();
        await Expect(Sidebar).Not.ToBeInViewportAsync();
        await Expect(Hamburger).ToHaveAttributeAsync("aria-expanded", "false");

        await Hamburger.ClickAsync();
        await Expect(Hamburger).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Sidebar).ToBeInViewportAsync();

        await Sidebar.Locator("#nav-textbox").ClickAsync();
        await Expect(Sidebar.Locator("#nav-textbox")).ToHaveAttributeAsync("aria-expanded", "true");
        await Sidebar.GetByRole(AriaRole.Link, new() { Name = "Playground" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/textbox/playground$"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync("NxTextBox");
        await Expect(Sidebar).Not.ToBeInViewportAsync();
    }

    [Test]
    public async Task Narrow_EscapeAndOverlayClickCloseTheDrawer()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/");

        await Hamburger.ClickAsync();
        await Expect(Sidebar).ToBeInViewportAsync();
        await Sidebar.Locator("#nav-button").FocusAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Sidebar).Not.ToBeInViewportAsync();

        await Hamburger.ClickAsync();
        await Page.Mouse.ClickAsync(330, 600);   // the backdrop, clear of the drawer and the scrollbar
        await Expect(Sidebar).Not.ToBeInViewportAsync();
    }

    [Test]
    public async Task Narrow_ClosedDrawerIsOutOfTheTabOrder()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/");

        await Expect(Sidebar).ToBeHiddenAsync();
    }

    [Test]
    public async Task Desktop_SidebarAlwaysVisible_TreeExpandsAndCollapses()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToAsync("/button/examples");

        await Expect(Hamburger).ToBeHiddenAsync();
        await Expect(Sidebar).ToBeInViewportAsync();

        // The current page's group is open and its page is marked.
        await Expect(Sidebar.Locator("#nav-button")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Sidebar.Locator("#nav-button-pages a.active")).ToHaveTextAsync("Examples");

        await Sidebar.Locator("#nav-combobox").ClickAsync();
        await Expect(Sidebar.Locator("#nav-combobox-pages a")).ToHaveTextAsync(new[] { "Overview", "Examples", "Playground", "API" });
        await Sidebar.Locator("#nav-combobox").ClickAsync();
        await Expect(Sidebar.Locator("#nav-combobox-pages")).ToHaveCountAsync(0);
    }

    [TestCase(390, 844)]
    [TestCase(1280, 900)]
    public async Task PageTabsAndPager_Navigate(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await GoToAsync("/button/overview");

        await Page.Locator(".doc-tabs").GetByRole(AriaRole.Link, new() { Name = "API" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/button/api$"));
        await Expect(Page.Locator(".doc-tab.active")).ToHaveTextAsync("API");

        // The next page is the overview of the component after NxButton in the nav, whatever that is today.
        var next = await Page.EvaluateAsync<string>(
            "() => { const ids = [...document.querySelectorAll('#site-nav [id^=nav-]:not([id$=-pages])')].map(e => e.id.slice(4)); return ids[ids.indexOf('button') + 1]; }");
        await Page.Locator(".doc-pager-next").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/{next}/overview$"));
        await Page.Locator(".doc-pager-prev").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/button/api$"));
    }

    [Test]
    public async Task OldRoutes_LandOnOverview()
    {
        await GoToAsync("/combobox");

        await Expect(Page.Locator("h1")).ToHaveTextAsync("NxComboBox");
        await Expect(Page.Locator(".doc-tab.active")).ToHaveCountAsync(0);
    }

    // ── Phone ergonomics ──────────────────────────────────────────────────────

    [Test]
    public async Task Phone_TouchTargetsAreAtLeast44px()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/button/playground");

        var hamburger = (await Hamburger.BoundingBoxAsync())!;
        Assert.That(hamburger.Height, Is.GreaterThanOrEqualTo(44));
        Assert.That(hamburger.Width, Is.GreaterThanOrEqualTo(44));

        var tab = (await Page.Locator(".doc-tab").First.BoundingBoxAsync())!;
        Assert.That(tab.Height, Is.GreaterThanOrEqualTo(44));

        var control = (await Page.Locator(".demo-control").First.BoundingBoxAsync())!;
        Assert.That(control.Height, Is.GreaterThanOrEqualTo(44));

        await Hamburger.ClickAsync();
        foreach (var link in await Sidebar.Locator("a, button").AllAsync())
            Assert.That((await link.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
    }

    [Test]
    public async Task Phone_PlaygroundStacksInOneColumn_And16pxGutters()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/textbox/playground");

        var preview = (await Page.Locator(".demo-preview").BoundingBoxAsync())!;
        var controls = (await Page.Locator(".demo-controls").BoundingBoxAsync())!;
        Assert.That(controls.Y, Is.GreaterThanOrEqualTo(preview.Y + preview.Height - 1), "controls are not below the preview");

        var h1 = (await Page.Locator("h1").BoundingBoxAsync())!;
        Assert.That(h1.X, Is.EqualTo(16).Within(1), "page gutter");
    }

    [Test]
    public async Task Phone_CodeBlocksAndTablesScrollInsideTheirBox()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/combobox/api");

        var wrap = Page.Locator(".doc-table-wrap").First;
        var scrolls = await wrap.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth");
        Assert.That(scrolls, Is.True, "test setup: the parameter table fits without scrolling");
        Assert.That(await HorizontalOverflowAsync(), Is.LessThanOrEqualTo(0));

        await GoToAsync("/combobox/examples");
        var code = Page.Locator(".doc-code").First;
        Assert.That(await code.EvaluateAsync<string>("el => getComputedStyle(el).overflowX"), Is.EqualTo("auto"));
        Assert.That(await HorizontalOverflowAsync(), Is.LessThanOrEqualTo(0));
    }

    [Test]
    public async Task Tablet_ExamplesUseTwoColumnsWhereTheyFit()
    {
        await Page.SetViewportSizeAsync(768, 1024);
        await GoToAsync("/textbox/examples");

        var grid = Page.Locator(".doc-section").Filter(new() { Has = Page.Locator("h2", new() { HasTextString = "Prefix" }) })
            .Locator(".demo-form-grid");
        var columns = await grid.EvaluateAsync<int>("el => getComputedStyle(el).gridTemplateColumns.split(' ').length");
        Assert.That(columns, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task Phone_ExamplesOnePerRow()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/textbox/examples");

        var grid = Page.Locator(".doc-section").Filter(new() { Has = Page.Locator("h2", new() { HasTextString = "Prefix" }) })
            .Locator(".demo-form-grid");
        var columns = await grid.EvaluateAsync<int>("el => getComputedStyle(el).gridTemplateColumns.split(' ').length");
        Assert.That(columns, Is.EqualTo(1));
    }
}
