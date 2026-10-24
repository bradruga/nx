using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class WindowTests : PageTest
{
    private readonly string _baseUrl;

    public WindowTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Window(string id) => Page.Locator($"#{id}");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/window/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<bool> ScrollLockedAsync() =>
        Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')");

    private async Task<int> ZAsync(string id) => await Window(id).EvaluateAsync<int>("e => +e.style.zIndex");

    [Test]
    public async Task Open_CenteredFocusedLocked_EscapeClosesAndReturnsFocus()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-open").ClickAsync();
        await Expect(Window("overview-window")).ToBeVisibleAsync();
        await Expect(Page.Locator("#overview-name")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.True);

        var box = (await Window("overview-window").BoundingBoxAsync())!;
        var width = await Page.EvaluateAsync<int>("() => document.documentElement.clientWidth");
        Assert.That(box.X + box.Width / 2, Is.EqualTo(width / 2.0).Within(2));
        Assert.That(box.Y + box.Height / 2, Is.EqualTo(Page.ViewportSize!.Height / 2.0).Within(2));

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("overview-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-open")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.False);
    }

    [Test]
    public async Task Modal_TabWrapsInsideTheWindow()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-open").ClickAsync();
        await Expect(Page.Locator("#overview-name")).ToBeFocusedAsync();
        // Close (title bar), field, Cancel, Save: four Tabs come back round.
        for (var i = 0; i < 4; i++)
            await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#overview-name")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(Window("overview-window").Locator(".nx-window-close")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(Page.Locator("#overview-save")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Footer_SaveCommitsAndCloses()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-open").ClickAsync();
        await Page.Locator("#overview-name").FillAsync("P-2000");
        await Page.Locator("#overview-save").ClickAsync();

        await Expect(Window("overview-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-result")).ToHaveTextAsync("Saved P-2000");
    }

    [Test]
    public async Task Backdrop_BlocksThePage()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-open").ClickAsync();
        await Expect(Window("overview-window")).ToBeVisibleAsync();

        var topElement = await Page.EvaluateAsync<string>("() => document.elementFromPoint(20, 400).className");
        Assert.That(topElement, Does.Contain("nx-window-backdrop"));
    }

    [Test]
    public async Task NonModal_PressBringsToFront_PageStaysUsable()
    {
        await GoToPage();

        await Page.Locator("#open-a").ClickAsync();
        await Page.Locator("#open-b").ClickAsync();
        Assert.That(await ZAsync("window-b"), Is.GreaterThan(await ZAsync("window-a")));
        Assert.That(await ScrollLockedAsync(), Is.False);

        await Window("window-a").Locator(".nx-window-title").ClickAsync();
        Assert.That(await ZAsync("window-a"), Is.GreaterThan(await ZAsync("window-b")));

        // The page behind still takes clicks.
        await Page.Locator("#open-modal").ClickAsync();
        await Expect(Window("modal-window")).ToBeVisibleAsync();
        Assert.That(await ZAsync("modal-window"), Is.GreaterThan(await ZAsync("window-a")));
    }

    [Test]
    public async Task Drag_ByTitleBar_StaysOnScreen()
    {
        await GoToPage();

        await Page.Locator("#open-a").ClickAsync();
        var start = (await Window("window-a").BoundingBoxAsync())!;
        var bar = (await Window("window-a").Locator(".nx-window-titlebar").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(bar.X + 60, bar.Y + 20);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(bar.X + 260, bar.Y + 120, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        var moved = (await Window("window-a").BoundingBoxAsync())!;
        Assert.That(moved.X - start.X, Is.EqualTo(200).Within(2));
        Assert.That(moved.Y - start.Y, Is.EqualTo(100).Within(2));

        bar = (await Window("window-a").Locator(".nx-window-titlebar").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(bar.X + 60, bar.Y + 20);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(bar.X + 60, 1, new() { Steps = 8 });
        await Page.Mouse.UpAsync();

        // The pointer went 19 px further up than the window's top can: the title bar stays on screen.
        Assert.That((await Window("window-a").BoundingBoxAsync())!.Y, Is.EqualTo(0).Within(1));
    }

    [Test]
    public async Task Resize_FromCorner()
    {
        await GoToPage();

        await Page.Locator("#open-actions").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-actions").ClickAsync();
        var before = (await Window("actions-window").BoundingBoxAsync())!;
        var handle = (await Window("actions-window").Locator(".nx-window-resize-se").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(handle.X + 7, handle.Y + 7);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X + 107, handle.Y + 57, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        var after = (await Window("actions-window").BoundingBoxAsync())!;
        Assert.That(after.Width - before.Width, Is.EqualTo(100).Within(2));
        Assert.That(after.Height - before.Height, Is.EqualTo(50).Within(2));
        Assert.That(after.X, Is.EqualTo(before.X).Within(1));
    }

    [Test]
    public async Task Resize_StopsAtMinWidth()
    {
        await GoToPage();

        await Page.Locator("#open-actions").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-actions").ClickAsync();
        var handle = (await Window("actions-window").Locator(".nx-window-resize-e").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(handle.X + 4, handle.Y + 10);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X - 500, handle.Y + 10, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        Assert.That((await Window("actions-window").BoundingBoxAsync())!.Width, Is.EqualTo(240).Within(1));
    }

    [Test]
    public async Task MinimizeKeepsContent_MaximizeFillsViewport()
    {
        await GoToPage();

        await Page.Locator("#open-actions").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-actions").ClickAsync();
        await Page.Locator("#actions-note").FillAsync("keep me");

        await Window("actions-window").Locator(".nx-window-minimize").ClickAsync();
        await Expect(Page.Locator("#actions-state")).ToContainTextAsync("Minimized: yes");
        await Expect(Window("actions-window").Locator(".nx-window-body")).ToBeHiddenAsync();
        await Window("actions-window").Locator(".nx-window-minimize").ClickAsync();
        await Expect(Page.Locator("#actions-note")).ToHaveValueAsync("keep me");

        await Window("actions-window").Locator(".nx-window-title").DblClickAsync();
        await Expect(Page.Locator("#actions-state")).ToContainTextAsync("Maximized: yes");
        var box = (await Window("actions-window").BoundingBoxAsync())!;
        Assert.That(box.X, Is.EqualTo(0).Within(1));
        Assert.That(box.Y, Is.EqualTo(0).Within(1));
        Assert.That(box.Height, Is.EqualTo(Page.ViewportSize!.Height).Within(1));

        await Window("actions-window").Locator(".nx-window-maximize").ClickAsync();
        await Expect(Page.Locator("#actions-state")).ToContainTextAsync("Maximized: no");
    }

    [Test]
    public async Task HeaderAction_Clicks()
    {
        await GoToPage();

        await Page.Locator("#open-actions").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-actions").ClickAsync();
        await Page.Locator("#actions-refresh").ClickAsync();

        await Expect(Page.Locator("#actions-state")).ToContainTextAsync("Refreshed × 1");
    }

    [Test]
    public async Task Nested_InnerOnTop_EscapeClosesInnerOnly()
    {
        await GoToPage();

        await Page.Locator("#open-outer").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-outer").ClickAsync();
        await Page.Locator("#open-inner").ClickAsync();
        await Expect(Window("inner-window")).ToBeVisibleAsync();
        Assert.That(await ZAsync("inner-window"), Is.GreaterThan(await ZAsync("outer-window")));

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("inner-window")).ToHaveCountAsync(0);
        await Expect(Window("outer-window")).ToBeVisibleAsync();
        await Expect(Page.Locator("#open-inner")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.True);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("outer-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#open-outer")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.False);
    }

    [Test]
    public async Task Closing_GuardCancels_BackdropCloses()
    {
        await GoToPage();

        await Page.Locator("#open-guarded").ClickAsync();
        // Focus inside the window means nx-window.js has opened it and listens for Escape.
        await Expect(Page.Locator("#guarded-dirty")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("#closing-log")).ToHaveTextAsync("Close cancelled: unsaved changes");
        await Expect(Window("guarded-window")).ToBeVisibleAsync();
        await Page.Locator("#guarded-discard").ClickAsync();
        await Expect(Window("guarded-window")).ToHaveCountAsync(0);

        await Page.Locator("#open-backdrop").ClickAsync();
        await Expect(Window("backdrop-window")).ToBeVisibleAsync();
        await Page.Mouse.ClickAsync(20, 400);
        await Expect(Window("backdrop-window")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Form_PopupsOpenAboveTheWindow_EscapeClosesPopupFirst()
    {
        await GoToPage();

        await Page.Locator("#open-form").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-form").ClickAsync();
        await Expect(Page.Locator("#form-customer")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("a");
        var popup = Page.Locator(".nx-combobox-popup");
        await Expect(popup).ToBeVisibleAsync();
        var onTop = await popup.EvaluateAsync<bool>("p => { const r = p.getBoundingClientRect(); return !!document.elementFromPoint(r.x + 20, r.y + 20).closest('.nx-combobox-popup'); }");
        Assert.That(onTop, Is.True);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(popup).ToHaveCountAsync(0);
        await Expect(Window("form-window")).ToBeVisibleAsync();

        await Page.Locator("#form-save").ClickAsync();
        await Expect(Window("form-window").Locator(".validation-message").First).ToHaveTextAsync("Pick a customer.");
        await Expect(Window("form-window")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ExplicitSizeAndPosition()
    {
        await GoToPage();

        await Page.Locator("#open-explicit").ClickAsync();
        var box = (await Window("explicit-window").BoundingBoxAsync())!;

        Assert.That(box.X, Is.EqualTo(40).Within(1));
        Assert.That(box.Y, Is.EqualTo(80).Within(1));
        Assert.That(box.Width, Is.EqualTo(720).Within(1));
        Assert.That(box.Height, Is.EqualTo(420).Within(1));
        var body = Window("explicit-window").Locator(".nx-window-body");
        Assert.That(await body.EvaluateAsync<bool>("b => b.scrollHeight > b.clientHeight"), Is.True);
    }

    [TestCase("small", 400)]
    [TestCase("medium", 600)]
    [TestCase("large", 900)]
    public async Task SizePresets(string size, int width)
    {
        await GoToPage();

        await Page.Locator($"#open-size-{size}").ClickAsync();

        Assert.That((await Window("size-window").BoundingBoxAsync())!.Width, Is.EqualTo(width).Within(1));
    }

    [Test]
    public async Task FocusRing_NotOnMouse_OnKeyboard()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-open").ClickAsync();
        await Expect(Page.Locator("#overview-name")).ToBeFocusedAsync();
        var close = Window("overview-window").Locator(".nx-window-close");
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(close).ToBeFocusedAsync();
        await Expect(close).ToHaveCSSAsync("outline-style", "solid");
    }

    [Test]
    public async Task Playground_NonModalAndClosableToggles()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-modal").UncheckAsync();
        await Page.Locator("#pg-closable").UncheckAsync();
        await Page.Locator("#pg-open").ClickAsync();
        await Expect(Window("playground-window")).ToBeVisibleAsync();
        await Expect(Page.Locator(".nx-window-backdrop")).ToHaveCountAsync(0);
        await Expect(Window("playground-window").Locator(".nx-window-close")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("playground-window")).ToBeVisibleAsync();
        await Page.Locator("#pg-ok").ClickAsync();
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Visible: false");
    }

    [Test]
    public async Task Phone_FullScreen_44pxButtons()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/window/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator("#overview-open").TapAsync();
        var window = page.Locator("#overview-window");
        await Expect(window).ToBeVisibleAsync();

        var box = (await window.BoundingBoxAsync())!;
        Assert.That(box.X, Is.EqualTo(0).Within(1));
        Assert.That(box.Y, Is.EqualTo(0).Within(1));
        Assert.That(box.Width, Is.EqualTo(390).Within(1));
        Assert.That(box.Height, Is.EqualTo(844).Within(1));
        var close = (await window.Locator(".nx-window-close").BoundingBoxAsync())!;
        Assert.That(close.Height, Is.GreaterThanOrEqualTo(44));

        await window.Locator(".nx-window-close").TapAsync();
        await Expect(window).ToHaveCountAsync(0);
    }
}
