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
        var area = await FixedArea.SizeAsync(Page);
        Assert.That(box.X + box.Width / 2, Is.EqualTo(area.Width / 2).Within(2));
        Assert.That(box.Y + box.Height / 2, Is.EqualTo(area.Height / 2).Within(2));

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

    /// <summary>The window is placed and sized in JS after it renders: wait until its box stops changing.</summary>
    private Task Settled(string id) => Window(id).EvaluateAsync(
        "w => new Promise(r => { let last = ''; const f = () => { const b = w.getBoundingClientRect(); const now = [b.left, b.top, b.width, b.height].join(); now === last ? r() : (last = now, requestAnimationFrame(f)); }; f(); })");

    [Test]
    public async Task Resize_FromCorner()
    {
        await GoToPage();

        await Page.Locator("#open-actions").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-actions").ClickAsync();
        await Settled("actions-window");
        var before = (await Window("actions-window").BoundingBoxAsync())!;
        var handle = (await Window("actions-window").Locator(".nx-window-resize-se").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(handle.X + 7, handle.Y + 7);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X + 107, handle.Y + 57, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        await Settled("actions-window");
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
        await Settled("actions-window");
        var handle = (await Window("actions-window").Locator(".nx-window-resize-e").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(handle.X + 4, handle.Y + 10);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X - 500, handle.Y + 10, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        await Window("actions-window").EvaluateAsync("w => new Promise(r => { const f = () => Math.abs(w.getBoundingClientRect().width - 240) <= 1 ? r() : requestAnimationFrame(f); f(); })");
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
        Assert.That(box.Height, Is.EqualTo((await FixedArea.SizeAsync(Page)).Height).Within(1));

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

    private async Task OpenNestedAsync()
    {
        await GoToPage();
        await Page.Locator("#open-outer").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-outer").ClickAsync();
        await Page.Locator("#open-inner").ClickAsync();
        await Expect(Window("inner-window")).ToBeVisibleAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('#inner-window').contains(document.activeElement)");
    }

    [Test]
    public async Task Nested_TabStaysInTheInnerWindow()
    {
        await OpenNestedAsync();

        // Forward and back, focus never leaves the top window for the one below or the page.
        foreach (var key in new[] { "Tab", "Tab", "Tab", "Tab", "Tab", "Shift+Tab", "Shift+Tab", "Shift+Tab", "Shift+Tab", "Shift+Tab", "Shift+Tab" })
        {
            await Page.Keyboard.PressAsync(key);
            Assert.That(await Page.EvaluateAsync<bool>("() => document.querySelector('#inner-window').contains(document.activeElement)"), Is.True, $"after {key}");
        }
    }

    [Test]
    public async Task Nested_CloseButtonsReturnFocusToTheirOpeners()
    {
        await OpenNestedAsync();

        // Closed with the mouse, not Escape: focus still goes back to the button that opened it.
        await Window("inner-window").Locator(".nx-window-close").ClickAsync();
        await Expect(Window("inner-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#open-inner")).ToBeFocusedAsync();
        await Window("outer-window").Locator(".nx-window-close").First.ClickAsync();
        await Expect(Window("outer-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#open-outer")).ToBeFocusedAsync();
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
        // nx-window.js places the window after it renders; read the box once it has moved.
        await Window("explicit-window").EvaluateAsync("w => new Promise(r => { const f = () => Math.abs(w.getBoundingClientRect().left - 40) < 1 ? r() : requestAnimationFrame(f); f(); })");
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

    // ── NxWindowHost (the demo's layout has one) ──────────────────────────────

    private Task<bool> InsideAsync(string id, string ancestor) =>
        Window(id).EvaluateAsync<bool>("(w, selector) => w.closest(selector) !== null", ancestor);

    [Test]
    public async Task Host_WindowUnderATransformedAncestor_IsCenteredAndNotClipped()
    {
        await GoToPage();

        await Page.Locator("#open-hosted").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-hosted").ClickAsync();
        await Expect(Window("hosted-window")).ToBeVisibleAsync();
        await Settled("hosted-window");

        // Declared inside the transformed, clipping panel; drawn outside it, in the layout's host.
        Assert.That(await InsideAsync("hosted-window", "#host-panel"), Is.False);
        var box = (await Window("hosted-window").BoundingBoxAsync())!;
        var area = await FixedArea.SizeAsync(Page);
        Assert.That(box.X + box.Width / 2, Is.EqualTo(area.Width / 2).Within(2));
        Assert.That(box.Y + box.Height / 2, Is.EqualTo(area.Height / 2).Within(2));

        // Every corner of it is on top: nothing cut off by the panel.
        var corners = await Window("hosted-window").EvaluateAsync<bool[]>(
            """
            w => {
                const b = w.getBoundingClientRect();
                return [[b.left + 3, b.top + 3], [b.right - 3, b.top + 3], [b.left + 3, b.bottom - 3], [b.right - 3, b.bottom - 3]]
                    .map(([x, y]) => w.contains(document.elementFromPoint(x, y)));
            }
            """);
        Assert.That(corners, Is.All.True);
        Assert.That(await ScrollLockedAsync(), Is.True);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("hosted-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#open-hosted")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.False);
    }

    [Test]
    public async Task Host_ExamplesAreDrawnInTheHost_NestedOnesSideBySide()
    {
        await GoToPage();

        await Page.Locator("#open-outer").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#open-outer").ClickAsync();
        await Page.Locator("#open-inner").ClickAsync();
        await Expect(Window("inner-window")).ToBeVisibleAsync();

        Assert.That(await InsideAsync("outer-window", ".main-content"), Is.False);
        Assert.That(await InsideAsync("inner-window", "#outer-window"), Is.False);
        Assert.That(await ZAsync("inner-window"), Is.GreaterThan(await ZAsync("outer-window")));
    }

    [Test]
    public async Task Host_LeavingThePageWithAWindowOpen_TakesItOutOfTheHost()
    {
        await GoToPage();

        await Page.Locator("#open-modal").ClickAsync();
        await Expect(Window("modal-window")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Page.Locator("#open-a").ClickAsync();
        await Expect(Window("window-a")).ToBeVisibleAsync();

        // The layout, and the host in it, stay; the page and its windows go.
        await Page.Locator("a.doc-tab[href='window/overview']").ClickAsync();
        await Expect(Page.Locator("#overview-open")).ToBeVisibleAsync();
        await Expect(Page.Locator(".nx-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        await Page.Locator("#overview-open").ClickAsync();
        await Expect(Window("overview-window")).ToBeVisibleAsync();
        await Expect(Page.Locator("#overview-name")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Inline_StaysWhereItIsDeclared_AndStillWorks()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-open").ClickAsync();
        await Expect(Window("playground-window")).ToBeVisibleAsync();
        Assert.That(await InsideAsync("playground-window", ".demo-preview-inner"), Is.False);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("playground-window")).ToHaveCountAsync(0);

        await Page.Locator("#pg-inline").CheckAsync();
        await Page.Locator("#pg-open").ClickAsync();
        await Expect(Window("playground-window")).ToBeVisibleAsync();
        Assert.That(await InsideAsync("playground-window", ".demo-preview-inner"), Is.True);
        await Expect(Page.Locator("#pg-field")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.True);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Window("playground-window")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#pg-open")).ToBeFocusedAsync();
        Assert.That(await ScrollLockedAsync(), Is.False);
    }

    [Test]
    public async Task Inline_ToggledWhileOpen_MovesTheOpenWindow()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-modal").UncheckAsync();
        await Page.Locator("#pg-open").ClickAsync();
        await Expect(Window("playground-window")).ToBeVisibleAsync();

        await Page.Locator("#pg-inline").CheckAsync();
        await Expect(Page.Locator(".demo-preview-inner #playground-window")).ToBeVisibleAsync();
        await Page.Locator("#pg-inline").UncheckAsync();
        await Expect(Page.Locator(".demo-preview-inner #playground-window")).ToHaveCountAsync(0);
        await Expect(Window("playground-window")).ToBeVisibleAsync();

        // Still the one open window to nx-window.js: its close button works and nothing is left locked.
        await Window("playground-window").Locator(".nx-window-close").ClickAsync();
        await Expect(Window("playground-window")).ToHaveCountAsync(0);
        Assert.That(await ScrollLockedAsync(), Is.False);
    }

    // ── OnMoved and OnResized ─────────────────────────────────────────────────

    [Test]
    public async Task MovedAndResized_AreReported_AndASavedPlaceIsRestored()
    {
        await GoToPage();

        await Page.Locator("#open-saved").ScrollIntoViewIfNeededAsync();
        await Expect(Page.Locator("#saved-place")).ToHaveTextAsync("Position: centered");
        await Page.Locator("#open-saved").ClickAsync();
        await Settled("saved-window");

        // A click on the title bar that moves nothing reports nothing.
        await Window("saved-window").Locator(".nx-window-title").ClickAsync();
        await Expect(Page.Locator("#saved-place")).ToHaveTextAsync("Position: centered");

        var bar = (await Window("saved-window").Locator(".nx-window-titlebar").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(bar.X + 60, bar.Y + 20);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(bar.X - 60, bar.Y - 40, new() { Steps = 5 });
        await Page.Mouse.UpAsync();
        await Settled("saved-window");
        var moved = (await Window("saved-window").BoundingBoxAsync())!;
        await Expect(Page.Locator("#saved-place")).ToHaveTextAsync($"Position: {Math.Round(moved.X)}px, {Math.Round(moved.Y)}px");
        await Expect(Page.Locator("#saved-size")).ToHaveTextAsync("Size: default");

        var handle = (await Window("saved-window").Locator(".nx-window-resize-se").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(handle.X + 7, handle.Y + 7);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X + 87, handle.Y + 47, new() { Steps = 5 });
        await Page.Mouse.UpAsync();
        await Settled("saved-window");
        var resized = (await Window("saved-window").BoundingBoxAsync())!;
        Assert.That(resized.Width - moved.Width, Is.EqualTo(80).Within(2));
        await Expect(Page.Locator("#saved-size")).ToHaveTextAsync($"Size: {Math.Round(resized.Width)}px × {Math.Round(resized.Height)}px");

        // Pulling the top-left corner resizes and moves: both are reported.
        handle = (await Window("saved-window").Locator(".nx-window-resize-nw").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(handle.X + 7, handle.Y + 7);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X - 13, handle.Y - 3, new() { Steps = 5 });
        await Page.Mouse.UpAsync();
        await Settled("saved-window");
        var last = (await Window("saved-window").BoundingBoxAsync())!;
        Assert.That(last.X - resized.X, Is.EqualTo(-20).Within(2));
        await Expect(Page.Locator("#saved-place")).ToHaveTextAsync($"Position: {Math.Round(last.X)}px, {Math.Round(last.Y)}px");
        await Expect(Page.Locator("#saved-size")).ToHaveTextAsync($"Size: {Math.Round(last.Width)}px × {Math.Round(last.Height)}px");

        await Window("saved-window").Locator(".nx-window-close").ClickAsync();
        await Expect(Window("saved-window")).ToHaveCountAsync(0);
        await Page.Locator("#open-saved").ClickAsync();
        await Settled("saved-window");

        var reopened = (await Window("saved-window").BoundingBoxAsync())!;
        Assert.That(reopened.X, Is.EqualTo(last.X).Within(1));
        Assert.That(reopened.Y, Is.EqualTo(last.Y).Within(1));
        Assert.That(reopened.Width, Is.EqualTo(last.Width).Within(1));
        Assert.That(reopened.Height, Is.EqualTo(last.Height).Within(1));
    }

    [Test]
    public async Task Playground_ShowsOnMoved()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-open").ClickAsync();
        await Settled("playground-window");
        var bar = (await Window("playground-window").Locator(".nx-window-titlebar").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(bar.X + 80, bar.Y + 20);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(bar.X + 120, bar.Y + 50, new() { Steps = 4 });
        await Page.Mouse.UpAsync();
        await Settled("playground-window");

        var box = (await Window("playground-window").BoundingBoxAsync())!;
        await Expect(Page.Locator("#playground-moved")).ToHaveTextAsync($"OnMoved: {Math.Round(box.X)}, {Math.Round(box.Y)}");
        await Expect(Page.Locator("#playground-resized")).ToHaveTextAsync("OnResized: —");
    }
}
