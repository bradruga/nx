using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxIcon, NxLoader and NxLoaderContainer.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class IconLoaderTests : PageTest
{
    private readonly string _baseUrl;

    public IconLoaderTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private static async Task<float> Width(ILocator l) => (await l.BoundingBoxAsync())!.Width;

    // ── NxIcon ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Icon_SizesFollowTheTokens()
    {
        await GoToPage("/icon/examples");

        var expected = new Dictionary<string, float> { ["XSmall"] = 12, ["Small"] = 14, ["Medium"] = 16, ["Large"] = 20, ["XLarge"] = 24, ["XXLarge"] = 32, ["XXXLarge"] = 48 };
        foreach (var (size, px) in expected)
            Assert.That(await Width(Page.Locator($"#icon-sizes svg[data-size={size}]")), Is.EqualTo(px).Within(0.5), size);
    }

    [Test]
    public async Task Icon_DrawsInTheTextColor_OrATokenColor()
    {
        await GoToPage("/icon/examples");

        var colors = await Page.EvaluateAsync<string[]>(
            "() => ['Inherit', 'Danger', 'Success'].map(c => getComputedStyle(document.querySelector(`#icon-colors svg[data-color=${c}]`)).color)");
        Assert.That(colors.Distinct().Count(), Is.EqualTo(3));
        var stroke = await Page.Locator("#icon-colors svg[data-color=Danger]").EvaluateAsync<string>("s => getComputedStyle(s).stroke");
        Assert.That(stroke, Is.EqualTo(colors[1]));
    }

    [Test]
    public async Task Icon_FilterAndPick()
    {
        await GoToPage("/icon/examples");

        await Page.Locator("#icon-filter").FillAsync("arrow");
        await Expect(Page.Locator("#icon-count")).ToHaveTextAsync(new Regex(@"^\d+ of \d+ icons$"));
        var names = await Page.Locator("#icon-grid .demo-icon-tile").EvaluateAllAsync<string[]>("ts => ts.map(t => t.dataset.name)");
        Assert.That(names, Is.Not.Empty);
        Assert.That(names, Has.All.Contains("Arrow"));

        await Page.Locator("#icon-grid .demo-icon-tile[data-name=ArrowLeft]").ClickAsync();
        await Expect(Page.Locator("#icon-picked")).ToHaveTextAsync("<NxIcon Icon=\"NxIcons.ArrowLeft\" />");
    }

    [Test]
    public async Task Icon_LabelledIsAnImage_OthersAreHidden()
    {
        await GoToPage("/icon/examples");

        await Expect(Page.GetByRole(AriaRole.Img, new() { Name = "Released" })).ToBeVisibleAsync();
        await Expect(Page.Locator("#icon-sizes svg").First).ToHaveAttributeAsync("aria-hidden", "true");
    }

    [Test]
    public async Task Icon_InAButton_IsCenteredOnTheLabel()
    {
        await GoToPage("/icon/overview");

        var button = Page.Locator(".nx-button", new() { HasText = "Add pour" });
        var icon = (await button.Locator("svg.nx-icon").BoundingBoxAsync())!;
        var box = (await button.BoundingBoxAsync())!;
        Assert.That(icon.Y + icon.Height / 2, Is.EqualTo(box.Y + box.Height / 2).Within(1.5));
    }

    // ── NxLoader ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Loader_SizesFollowTheTokens_AndAnimate()
    {
        await GoToPage("/loader/examples");

        Assert.That(await Width(Page.Locator("#loader-matrix [data-type=Spinner][data-size=Small]")), Is.EqualTo(16).Within(0.5));
        Assert.That(await Width(Page.Locator("#loader-matrix [data-type=Spinner][data-size=Medium]")), Is.EqualTo(28).Within(0.5));
        Assert.That(await Width(Page.Locator("#loader-matrix [data-type=Spinner][data-size=Large]")), Is.EqualTo(44).Within(0.5));

        var animation = await Page.Locator("#loader-matrix [data-type=Spinner][data-size=Large] .nx-loader-ring")
            .EvaluateAsync<string>("r => getComputedStyle(r).animationName");
        Assert.That(animation, Does.Contain("nx-loader-turn"));
    }

    [Test]
    public async Task Container_CoversTheArea_BlocksClicks_ThenGoes()
    {
        await GoToPage("/loader/examples");

        await Page.Locator("#card-load").ClickAsync();
        var overlay = Page.Locator("#card-loader");
        await Expect(overlay).ToBeVisibleAsync();

        var area = (await Page.Locator("#card-area").BoundingBoxAsync())!;
        var cover = (await overlay.BoundingBoxAsync())!;
        Assert.That(cover.Width, Is.EqualTo(area.Width - 2).Within(1));
        Assert.That(cover.Height, Is.EqualTo(area.Height - 2).Within(1));

        // A click on the card's button lands on the overlay instead.
        var button = (await Page.Locator("#card-button").BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(button.X + button.Width / 2, button.Y + button.Height / 2);
        await Expect(Page.Locator("#card-clicks")).ToHaveTextAsync("Card button clicks: 0");

        await Expect(overlay).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Page.Locator("#card-button").ClickAsync();
        await Expect(Page.Locator("#card-clicks")).ToHaveTextAsync("Card button clicks: 1");
    }

    [Test]
    public async Task Container_OverlayColorFromTheToken()
    {
        await GoToPage("/loader/examples");

        var bg = await Page.Locator("#dark-area .nx-loader-container").EvaluateAsync<string>("o => getComputedStyle(o).backgroundColor");
        Assert.That(bg, Is.EqualTo("rgb(37, 40, 46)"));
    }

    [Test]
    public async Task Container_CustomContent()
    {
        await GoToPage("/loader/examples");
        await Expect(Page.Locator("#custom-area .nx-loader-container-panel")).ToContainTextAsync("Scanning documents");
        await Expect(Page.Locator("#custom-area .nx-loader-container-panel")).ToHaveAttributeAsync("role", "status");
    }

    [Test]
    public async Task Playground_VisibleAndText()
    {
        await GoToPage("/loader/playground");

        await Expect(Page.Locator("#playground-area .nx-loader-container-text")).ToHaveTextAsync("Loading…");
        await Page.Locator("#pg-visible").UncheckAsync();
        await Expect(Page.Locator("#playground-area .nx-loader-container")).ToHaveCountAsync(0);

        await Page.Locator("#pg-container").UncheckAsync();
        await Page.Locator("#pg-type").SelectOptionAsync("Pulsing");
        await Expect(Page.Locator("#playground-loader .nx-loader-dot")).ToHaveCountAsync(3);
    }
}
