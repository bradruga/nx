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

    /// <summary>Drags from the middle of the gradient's area to its top-right corner (full saturation and brightness).</summary>
    private async Task DragAreaToTopRightAsync(string id)
    {
        var area = Page.Locator($"#{id} .nx-colorgradient-area");
        await area.ScrollIntoViewIfNeededAsync();
        var box = (await area.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width * 0.5f, box.Y + box.Height * 0.5f);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width, box.Y, new() { Steps = 6 });
        await Page.Mouse.UpAsync();
    }

    [Test]
    public async Task Gradient_Examples_DragMovesThumb_HexAndBoundValue()
    {
        await GoToPage("/colorgradient/examples");

        // White has hue 0: the top-right corner is pure red.
        await DragAreaToTopRightAsync("text-gradient");

        await Expect(Page.Locator("#text-gradient-hex")).ToHaveValueAsync("#ff0000");
        await Expect(Page.Locator("#initials-value")).ToHaveTextAsync("#ff0000 on #2e6db4");
        await Expect(Page.Locator("#initials-preview")).ToHaveCSSAsync("color", "rgb(255, 0, 0)");
        var area = (await Page.Locator("#text-gradient .nx-colorgradient-area").BoundingBoxAsync())!;
        var thumb = (await Page.Locator("#text-gradient .nx-colorgradient-thumb").BoundingBoxAsync())!;
        Assert.That(thumb.X + thumb.Width / 2, Is.EqualTo(area.X + area.Width).Within(1.5));
        Assert.That(thumb.Y + thumb.Height / 2, Is.EqualTo(area.Y).Within(1.5));
    }

    [Test]
    public async Task Gradient_ArrowDownPageDown_StepBrightness_PageStays()
    {
        await GoToPage("/colorgradient/examples");
        var thumb = Page.Locator("#text-gradient .nx-colorgradient-thumb");
        await thumb.ScrollIntoViewIfNeededAsync();
        // nx-colorgradient.js cancels the keys' scrolling one round trip after the page turns interactive.
        await thumb.EvaluateAsync("""
            t => new Promise(done => { const probe = () => { const e = new KeyboardEvent('keydown', { key: 'PageDown', cancelable: true });
                 t.dispatchEvent(e); e.defaultPrevented ? done() : setTimeout(probe, 50); }; probe(); })
            """);
        await Expect(thumb).ToHaveAttributeAsync("aria-valuetext", "Saturation 0%, brightness 100%");

        await thumb.FocusAsync();
        var scroll = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(thumb).ToHaveAttributeAsync("aria-valuetext", "Saturation 0%, brightness 99%");
        await Page.Keyboard.PressAsync("PageDown");
        await Expect(thumb).ToHaveAttributeAsync("aria-valuetext", "Saturation 0%, brightness 89%");

        await Expect(Page.Locator("#text-gradient-hex")).Not.ToHaveValueAsync("#ffffff");
        await Expect(thumb).ToBeFocusedAsync();
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll), "the keys do not scroll the page");
    }

    [Test]
    public async Task Gradient_HueSliderDrag_RepaintsTheArea()
    {
        await GoToPage("/colorgradient/examples");
        var hue = Page.Locator("#background-gradient .nx-colorgradient-hue");
        await hue.ScrollIntoViewIfNeededAsync();
        var start = await hue.InputValueAsync();

        var box = (await hue.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width * 0.9f, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width / 3, box.Y + box.Height / 2, new() { Steps = 8 });
        await Page.Mouse.UpAsync();

        var end = int.Parse(await hue.InputValueAsync());
        Assert.That(end, Is.Not.EqualTo(int.Parse(start)));
        Assert.That(end, Is.InRange(100, 140), "a third of the way along is about green");
        // The round trip writes the hue onto the root, and the area's gradient is painted in it.
        await Page.WaitForFunctionAsync("""
            () => { const root = document.getElementById('background-gradient');
                    const hue = root.querySelector('.nx-colorgradient-hue').value;
                    if (root.style.getPropertyValue('--nx-colorgradient-hue').trim() !== hue) return false;
                    const probe = document.createElement('span'); probe.style.color = `hsl(${hue}deg 100% 50%)`;
                    document.body.append(probe); const color = getComputedStyle(probe).color; probe.remove();
                    return getComputedStyle(root.querySelector('.nx-colorgradient-area')).backgroundImage.includes(color); }
            """);
        await Expect(Page.Locator("#initials-value")).Not.ToHaveTextAsync("#ffffff on #2e6db4");
    }

    [Test]
    public async Task Gradient_InADialog_DragThenOk_SetsTheValue()
    {
        await GoToPage("/colorgradient/examples");
        var swatch = Page.Locator("#popup-swatch");
        await swatch.ScrollIntoViewIfNeededAsync();
        await swatch.ClickAsync();
        await Expect(Page.Locator("#popup-gradient")).ToBeVisibleAsync();
        var hex = Page.Locator("#popup-gradient-hex");
        await Expect(hex).ToHaveValueAsync("#c0392b");

        await DragAreaToTopRightAsync("popup-gradient");
        await Expect(hex).Not.ToHaveValueAsync("#c0392b");
        var picked = await hex.InputValueAsync();
        await Expect(Page.Locator("#popup-value")).ToHaveTextAsync("#c0392b", new() { Timeout = 1000 });

        await Page.Locator("#popup-ok").ClickAsync();
        await Expect(Page.Locator("#popup-gradient")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#popup-value")).ToHaveTextAsync(picked);
        Assert.That(picked, Does.Match("^#ff[0-9a-f]{4}$"), "full saturation and brightness of the red hue");
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
