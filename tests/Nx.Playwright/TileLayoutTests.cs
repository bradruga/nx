using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxTileLayout: dragging, resizing and narrow stacking need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TileLayoutTests : PageTest
{
    private readonly string _baseUrl;

    public TileLayoutTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/tilelayout/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Tile(string id) => Page.Locator($"#edit-tiles [data-nx-tile={id}]");

    private Task<string[]> OrderAsync() =>
        Page.Locator("#edit-tiles [data-nx-tile]").EvaluateAllAsync<string[]>("ts => ts.map(t => t.dataset.nxTile)");

    [Test]
    public async Task Spans_PlaceTilesOnTheGrid()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");

        var reminders = (await Page.Locator("#overview-tiles [data-nx-tile=reminders]").BoundingBoxAsync())!;
        var matters = (await Page.Locator("#overview-tiles [data-nx-tile=matters]").BoundingBoxAsync())!;
        var documents = (await Page.Locator("#overview-tiles [data-nx-tile=documents]").BoundingBoxAsync())!;

        Assert.That(matters.Y, Is.EqualTo(reminders.Y).Within(1), "2 + 3 columns share the first row");
        Assert.That(matters.Width / reminders.Width, Is.EqualTo(1.5).Within(0.1));
        Assert.That(documents.Y, Is.GreaterThan(reminders.Y + reminders.Height - 1));
        Assert.That(documents.Width, Is.EqualTo(matters.X + matters.Width - reminders.X).Within(1));
        Assert.That(reminders.Height, Is.EqualTo(5 * 50 + 4 * 16).Within(1), "5 rows of 50 px and their gaps");
    }

    [Test]
    public async Task DragHeader_MovesTheTile()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");

        await Tile("email").Locator(".nx-tile-header").DragToAsync(Tile("sets").Locator(".nx-tile-header"),
            new() { SourcePosition = new() { X = 40, Y = 20 }, TargetPosition = new() { X = 60, Y = 20 } });

        await Expect(Page.Locator("#edit-log")).ToHaveTextAsync("Moved email from 5 to 1");
        Assert.That(await OrderAsync(), Is.EqualTo(new[] { "email", "sets", "products", "cad", "tomorrow" }));
    }

    [Test]
    public async Task ResizeHandle_ChangesTheSpans()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");

        var handle = Tile("cad").Locator(".nx-tile-resize");
        await handle.ScrollIntoViewIfNeededAsync();
        var box = (await handle.BoundingBoxAsync())!;
        var column = (await Tile("products").BoundingBoxAsync())!.Width;
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + column + 16, box.Y + 120 + 16, new() { Steps = 8 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator("#edit-log")).ToHaveTextAsync("Resized cad to 2×2");
        await Expect(Tile("cad")).ToHaveAttributeAsync("style", "--nx-tile-cols: 2; --nx-tile-rows: 2;");
    }

    [Test]
    public async Task Keyboard_MovesKeepsFocus_PageDoesNotScroll()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");

        var header = Tile("sets").Locator(".nx-tile-header");
        await header.FocusAsync();
        var scroll = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.Keyboard.PressAsync("Control+ArrowRight");

        await Expect(Page.Locator("#edit-log")).ToHaveTextAsync("Moved sets from 1 to 2");
        await Expect(Tile("sets").Locator(".nx-tile-header")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await Expect(Tile("sets")).ToHaveAttributeAsync("style", "--nx-tile-cols: 2; --nx-tile-rows: 3;");
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll));
        await Expect(Page.Locator("#edit-tiles .nx-tilelayout-sr[role=status]")).ToHaveTextAsync("Today's Sets is 2 columns wide and 3 rows tall.");
    }

    [Test]
    public async Task SaveHideRestore()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");

        await Page.Locator("#edit-save").ClickAsync();
        await Expect(Page.Locator("#edit-state")).ToContainTextAsync("0: sets");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Hide CAD Required" }).ClickAsync();
        await Expect(Tile("cad")).ToHaveCountAsync(0);
        await Page.Locator("#edit-restore").ClickAsync();
        await Expect(Tile("cad")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Narrow_StacksInOneColumn_NoHandles()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("examples");

        var widths = await Page.Locator("#edit-tiles [data-nx-tile]").EvaluateAllAsync<double[]>("ts => ts.map(t => t.getBoundingClientRect().width)");
        Assert.That(widths.Distinct().Count(), Is.EqualTo(1), "every tile the full width");
        await Expect(Tile("sets").Locator(".nx-tile-resize")).ToBeHiddenAsync();
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }
}
