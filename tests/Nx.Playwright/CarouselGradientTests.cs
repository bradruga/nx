using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxCarousel and NxColorGradient.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class CarouselGradientTests : PageTest
{
    private readonly string _baseUrl;

    public CarouselGradientTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    /// <summary>Waits for the slide transition to end before measuring.</summary>
    private static Task SettledAsync(ILocator carousel) =>
        carousel.Locator(".nx-carousel-track").EvaluateAsync("e => Promise.all(e.getAnimations().map(a => a.finished))");

    // ── NxCarousel ────────────────────────────────────────────────────────────

    [Test]
    public async Task Carousel_ArrowsDotsKeys_SlideIntoView()
    {
        await GoToPage("/carousel/examples");
        var carousel = Page.Locator("#code-carousel");
        var page = Page.Locator("#code-page");

        await Page.Locator("#code-next").ClickAsync();
        await Expect(page).ToHaveTextAsync("Page 2");
        await SettledAsync(carousel);
        var viewport = (await carousel.BoundingBoxAsync())!;
        var active = (await carousel.Locator(".nx-carousel-slide-active").BoundingBoxAsync())!;
        Assert.That(active.X, Is.EqualTo(viewport.X).Within(1), "the active slide fills the viewport");

        await Page.Locator("#code-last").ClickAsync();
        await Expect(carousel.Locator(".nx-carousel-slide-active")).ToContainTextAsync("St. Anne's School");
        await carousel.Locator(".nx-carousel-dot").First.ClickAsync();
        await Expect(page).ToHaveTextAsync("Page 1");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(page).ToHaveTextAsync("Page 5", new() { Timeout = 3000 });
    }

    [Test]
    public async Task Carousel_RotatesByItself_PausesOnHover()
    {
        await GoToPage("/carousel/playground");
        await Page.Locator("#pg-interval").FillAsync("1000");
        await Page.Locator("#pg-interval").PressAsync("Tab");
        await Page.Mouse.MoveAsync(0, 0);

        await Expect(Page.Locator("#playground-page strong")).Not.ToHaveTextAsync("1", new() { Timeout = 4000 });

        await Page.Locator("#playground-carousel").HoverAsync();
        var shown = await Page.Locator("#playground-page strong").TextContentAsync();
        await Page.WaitForTimeoutAsync(2200);
        await Expect(Page.Locator("#playground-page strong")).ToHaveTextAsync(shown!);
    }

    [Test]
    public async Task Carousel_PauseButton_AndStoppedByBinding()
    {
        await GoToPage("/carousel/examples");

        await Page.Locator("#spotlight-carousel .nx-carousel-slide-active button").ClickAsync();
        await Expect(Page.Locator("#spotlight-state")).ToContainTextAsync("rotation stopped");

        await GoToPage("/carousel/overview");
        var pause = Page.Locator("#overview-carousel .nx-carousel-pause");
        await pause.ClickAsync();
        await Expect(pause).ToHaveAttributeAsync("aria-label", "Start slide rotation");
    }

    [Test]
    public async Task Carousel_Swipe()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/carousel/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var viewport = page.Locator("#code-carousel .nx-carousel-viewport");
        await viewport.ScrollIntoViewIfNeededAsync();
        // A 200 px swipe to the left, as touch pointer events built in the page.
        await viewport.EvaluateAsync(@"e => {
            const r = e.getBoundingClientRect();
            const at = x => ({ clientX: r.left + x, clientY: r.top + 50, pointerId: 1, pointerType: 'touch', isPrimary: true, bubbles: true });
            e.dispatchEvent(new PointerEvent('pointerdown', at(300)));
            e.dispatchEvent(new PointerEvent('pointerup', at(100)));
        }");

        await Expect(page.Locator("#code-page")).ToHaveTextAsync("Page 2");
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    // ── NxColorGradient ───────────────────────────────────────────────────────

    [Test]
    public async Task Gradient_DragInTheArea_FollowsThePointer()
    {
        await GoToPage("/colorgradient/overview");

        var area = Page.Locator("#overview-gradient .nx-colorgradient-area");
        var box = (await area.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width * 0.5f, box.Y + box.Height * 0.5f);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width, box.Y, new() { Steps = 6 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator("#overview-value strong")).ToHaveTextAsync("#0078ff");
        await Expect(Page.Locator("#overview-gradient .nx-colorgradient-thumb")).ToBeFocusedAsync();
        var thumb = (await Page.Locator("#overview-gradient .nx-colorgradient-thumb").BoundingBoxAsync())!;
        Assert.That(thumb.X + thumb.Width / 2, Is.EqualTo(box.X + box.Width).Within(1.5));
        Assert.That(thumb.Y + thumb.Height / 2, Is.EqualTo(box.Y).Within(1.5));
    }

    [Test]
    public async Task Gradient_Keys_AndHexInput()
    {
        await GoToPage("/colorgradient/overview");

        await Page.Locator("#overview-gradient .nx-colorgradient-thumb").FocusAsync();
        var scroll = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("PageUp");
        await Page.Keyboard.PressAsync("PageUp");
        await Page.Keyboard.PressAsync("PageUp");
        await Expect(Page.Locator("#overview-value strong")).ToHaveTextAsync("#0078ff");
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll), "the keys do not scroll the page");

        var hex = Page.Locator("#overview-gradient-hex");
        await hex.FillAsync("c0392b");
        await hex.PressAsync("Enter");
        await Expect(Page.Locator("#overview-value strong")).ToHaveTextAsync("#c0392b");
        await Expect(hex).ToHaveValueAsync("#c0392b");
    }

    [Test]
    public async Task Gradient_Phone_FitsTheWidth()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("/colorgradient/examples");

        var gradient = (await Page.Locator("#rgba-gradient").BoundingBoxAsync())!;
        Assert.That(gradient.Width, Is.LessThanOrEqualTo(390 - 32));
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }
}
