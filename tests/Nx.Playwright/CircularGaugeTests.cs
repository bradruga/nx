using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxCircularGauge: the drawn arc and its animation in a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class CircularGaugeTests : PageTest
{
    private readonly string _baseUrl;

    public CircularGaugeTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/circulargauge/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Buttons_MoveTheArc_ClampAtMax()
    {
        await GoToPage("overview");
        var gauge = Page.Locator("#overview-gauge");
        await Expect(gauge).ToHaveAttributeAsync("aria-valuenow", "64");
        await Expect(gauge.Locator(".nx-gauge-pointer")).ToHaveAttributeAsync("stroke-dashoffset", "36");

        await Page.Locator("#overview-more").ClickAsync();
        await Expect(gauge).ToHaveAttributeAsync("aria-valuenow", "79");
        await Expect(gauge.Locator(".nx-gauge-center")).ToContainTextAsync("79%");

        await Page.Locator("#overview-more").ClickAsync();
        await Page.Locator("#overview-more").ClickAsync();
        await Expect(gauge).ToHaveAttributeAsync("aria-valuenow", "100");
    }

    [Test]
    public async Task Ring_IsSquare_AtItsSize()
    {
        await GoToPage("overview");
        var box = (await Page.Locator("#overview-gauge").BoundingBoxAsync())!;
        Assert.That(box.Width, Is.EqualTo(200).Within(1));
        Assert.That(box.Height, Is.EqualTo(200).Within(1));
    }

    [Test]
    public async Task Loading_RunsToDone()
    {
        await GoToPage("examples");
        await Page.Locator("#loading-start").ClickAsync();
        await Expect(Page.Locator("#loading-gauge .nx-gauge-center")).ToHaveTextAsync("Done", new() { Timeout = 10000 });
        await Expect(Page.Locator("#loading-value")).ToHaveTextAsync("100%");
    }
}
