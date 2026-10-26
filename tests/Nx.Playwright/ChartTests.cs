using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxChart: measuring, hit-testing the pointer and resizing need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ChartTests : PageTest
{
    private readonly string _baseUrl;

    public ChartTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/chart/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Tooltip(string chart) => Page.Locator($"#{chart} .nx-chart-tooltip");

    [Test]
    public async Task Line_HoverNearAPoint_ShowsItsTooltip_LeaveHides()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");

        var marker = Page.Locator("#overview-chart circle[data-p='0-14']");
        await marker.ScrollIntoViewIfNeededAsync();
        var box = (await marker.BoundingBoxAsync())!;
        // Beside the point, not on it: the nearest point in the category under the pointer wins.
        await Page.Mouse.MoveAsync(box.X + box.Width / 2 + 3, box.Y + 40);

        await Expect(Tooltip("overview-chart")).ToContainTextAsync("10/8");
        await Expect(Tooltip("overview-chart")).ToContainTextAsync("Loads: 19");
        await Expect(Page.Locator("#overview-chart .nx-chart-guide")).ToHaveCountAsync(1);

        await Page.Mouse.MoveAsync(5, 5);
        await Expect(Tooltip("overview-chart")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Line_Keyboard_MovesThroughPoints()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-chart .nx-chart-plot").FocusAsync();
        var scroll = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Tooltip("overview-chart")).ToContainTextAsync("Loads: 9");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Tooltip("overview-chart")).ToContainTextAsync("9/25");
        await Page.Keyboard.PressAsync("End");
        await Expect(Tooltip("overview-chart")).ToContainTextAsync("10/23");
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll));
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Tooltip("overview-chart")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Resize_RedrawsToTheNewWidth()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");
        var svg = Page.Locator("#overview-chart svg");
        var wide = double.Parse((await svg.GetAttributeAsync("width"))!, System.Globalization.CultureInfo.InvariantCulture);

        await Page.SetViewportSizeAsync(500, 900);

        await Page.WaitForFunctionAsync("w => parseFloat(document.querySelector('#overview-chart svg').getAttribute('width')) < w - 100", wide);
        var plot = (await Page.Locator("#overview-chart .nx-chart-plot").BoundingBoxAsync())!;
        var narrow = double.Parse((await svg.GetAttributeAsync("width"))!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(narrow, Is.EqualTo(plot.Width).Within(1), "the drawing fills the plot exactly");
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }

    [Test]
    public async Task Legend_HidesASeries()
    {
        await GoToPage("examples");
        var chart = Page.Locator("#axes-chart");
        await Expect(chart.Locator("path.nx-chart-area")).ToHaveCountAsync(1);

        var hours = chart.GetByRole(AriaRole.Button, new() { Name = "Crane hours" });
        await hours.ClickAsync();

        await Expect(hours).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(chart.Locator("path.nx-chart-area")).ToHaveCountAsync(0);
        await hours.ClickAsync();
        await Expect(chart.Locator("path.nx-chart-area")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Click_RaisesWithTheDataItem()
    {
        await GoToPage("examples");

        await Page.Locator("#control-chart circle[data-p='0-6']").ClickAsync(new() { Force = true });

        await Expect(Page.Locator("#control-picked")).ToHaveTextAsync("Test T-4107: 5.59 in");
    }

    [Test]
    public async Task Pie_HoverASlice_TooltipWithShare_Pops()
    {
        await GoToPage("examples");
        var slice = Page.Locator("#pie-chart path[data-p='0-1']");
        await slice.ScrollIntoViewIfNeededAsync();

        await slice.HoverAsync();

        await Expect(Tooltip("pie-chart")).ToHaveTextAsync("South yard: 288 (32.7%)");
        await Expect(slice).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-chart-active"));
    }

    [Test]
    public async Task Bars_LabelsStayInsideTheChart()
    {
        await GoToPage("examples");
        var chart = Page.Locator("#bar-chart");
        await chart.ScrollIntoViewIfNeededAsync();

        var svg = (await chart.Locator("svg").BoundingBoxAsync())!;
        foreach (var label in await chart.Locator(".nx-chart-value-labels text").AllAsync())
        {
            var box = (await label.BoundingBoxAsync())!;
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(svg.X + svg.Width + 1));
        }
        await Expect(chart.Locator(".nx-chart-value-labels text").First).ToHaveTextAsync("1,240");
    }

    [Test]
    public async Task Phone_TapShowsTheTooltip_LegendWraps()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/chart/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var bar = page.Locator("#column-chart rect[data-p='0-2']");
        await bar.ScrollIntoViewIfNeededAsync();
        await bar.TapAsync();

        await Expect(page.Locator("#column-chart .nx-chart-tooltip")).ToContainTextAsync("Plant 1");
        await Expect(page.Locator("#pie-chart .nx-chart-body")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-chart-legend-bottom"));
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    [Test]
    public async Task Playground_TypeSwitch_DrawsTheNewType()
    {
        await GoToPage("playground");
        var chart = Page.Locator("#playground-chart");

        await Page.Locator("#pg-type").SelectOptionAsync("Bar");
        await Expect(chart.Locator("rect.nx-chart-bar")).ToHaveCountAsync(14);
        await Page.Locator("#pg-type").SelectOptionAsync("Pie");
        await Expect(chart.Locator("path.nx-chart-slice")).ToHaveCountAsync(7);
        await Expect(chart.Locator(".nx-chart-legend-item")).ToHaveCountAsync(7);
    }
}
