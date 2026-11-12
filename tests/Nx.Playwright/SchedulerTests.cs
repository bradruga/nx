using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxScheduler: placing items in real layout, the sticky headers and the opening scroll need a browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SchedulerTests : PageTest
{
    private readonly string _baseUrl;

    public SchedulerTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page, int width = 1280)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync($"{_baseUrl}/scheduler/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Opens_AtTheWorkDay()
    {
        await GoToPage("overview");
        var grid = Page.Locator("#overview-scheduler .nx-scheduler-grid");
        await Page.WaitForFunctionAsync("() => document.querySelector('#overview-scheduler .nx-scheduler-grid').scrollTop > 0");
        // 7 rows of 40 px: 7:00.
        Assert.That(await grid.EvaluateAsync<double>("g => g.scrollTop"), Is.EqualTo(280).Within(2));
    }

    [Test]
    public async Task Items_SitOnTheirHours()
    {
        await GoToPage("overview");
        var monday = Page.Locator("#overview-scheduler .nx-scheduler-day").Nth(1);
        var day = monday.Locator(".nx-scheduler-event").Filter(new() { HasText = "Day" }).First;
        var slot7 = monday.Locator(".nx-scheduler-slot").Nth(7);
        var a = (await day.BoundingBoxAsync())!;
        var b = (await slot7.BoundingBoxAsync())!;
        Assert.That(a.Y, Is.EqualTo(b.Y).Within(1));
        Assert.That(a.Height, Is.EqualTo(8 * b.Height).Within(2));
    }

    [Test]
    public async Task Header_StaysAlignedAndSticky()
    {
        await GoToPage("overview");
        var grid = Page.Locator("#overview-scheduler .nx-scheduler-grid");
        var head = Page.Locator("#overview-scheduler .nx-scheduler-day-head").Nth(3);
        var column = Page.Locator("#overview-scheduler .nx-scheduler-day").Nth(3);
        Assert.That((await head.BoundingBoxAsync())!.X, Is.EqualTo((await column.BoundingBoxAsync())!.X).Within(1));

        var before = (await head.BoundingBoxAsync())!.Y;
        await grid.EvaluateAsync("g => g.scrollTop = 600");
        Assert.That((await head.BoundingBoxAsync())!.Y, Is.EqualTo(before).Within(1));
    }

    [Test]
    public async Task Arrows_MoveTheWeek_AndTheBoundPicker()
    {
        await GoToPage("examples");
        var range = Page.Locator("#date-scheduler .nx-scheduler-range");
        var start = await range.TextContentAsync();
        await Page.Locator("#date-scheduler [data-nx-scheduler=next]").ClickAsync();
        await Expect(range).Not.ToHaveTextAsync(start!);
        await Expect(Page.Locator("#date-picker")).ToHaveValueAsync(DateTime.Today.AddDays(7).ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture));

        await Page.Locator("#date-scheduler [data-nx-scheduler=today]").ClickAsync();
        await Expect(range).ToHaveTextAsync(start!);
        await Expect(Page.Locator("#date-scheduler [data-nx-scheduler=today]")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Overlaps_SideBySide()
    {
        await GoToPage("examples");
        var styles = await Page.Locator("#meetings-scheduler .nx-scheduler-day").Nth(1).Locator(".nx-scheduler-event")
            .EvaluateAllAsync<string[]>("es => es.map(e => e.style.left + '/' + e.style.width)");
        Assert.That(styles, Is.EqualTo(new[] { "0%/50%", "50%/50%", "0%/50%" }));
    }

    [Test]
    public async Task Phone_ScrollsInside_NotThePage()
    {
        await GoToPage("overview", 390);
        var grid = Page.Locator("#overview-scheduler .nx-scheduler-grid");
        Assert.That(await grid.EvaluateAsync<bool>("g => g.scrollWidth > g.clientWidth"), Is.True);
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }
}
