using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ComboBoxTests : PageTest
{
    private readonly string _baseUrl;

    public ComboBoxTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Section(string heading) =>
        Page.Locator(".doc-section").Filter(new() { Has = Page.Locator("h2", new() { HasTextString = heading }) });

    private ILocator Input(string id)   => Page.Locator($"#{id}");
    private ILocator Field(string id)   => Page.Locator(".nx-combobox-field").Filter(new() { Has = Input(id) });
    private ILocator Combo(string id)   => Page.Locator(".nx-combobox").Filter(new() { Has = Input(id) });
    private ILocator List(string id)    => Page.Locator($"#{id}-list");
    private ILocator Options(string id) => List(id).Locator("[role=option]");
    private ILocator Popup              => Page.Locator(".nx-combobox-popup");

    private async Task GoToPage(string page = "examples")
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await Page.GotoAsync(_baseUrl + $"/combobox/{page}");
        // The page is prerendered; the combo boxes only respond once the circuit is live.
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private async Task OpenAsync(string id)
    {
        await Input(id).ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Input(id)).ToHaveAttributeAsync("aria-expanded", "true");
        // The popup renders hidden and is revealed by JS once placed.
        await Expect(Popup).ToBeVisibleAsync();
    }

    /// <summary>Waits for the highlighted option to read <paramref name="text"/>; key presses reach the server asynchronously.</summary>
    private Task ExpectActiveAsync(string id, string text) =>
        Expect(List(id).Locator(".nx-combobox-item-active")).ToHaveTextAsync(text);

    /// <summary>
    /// The height of a virtualized list's first row and the gap to the second, read in one frame once both are there:
    /// Virtualize settles on its window over a few renders (row 0 comes and goes), and a popup above its field grows meanwhile.
    /// </summary>
    private async Task<(double Height, double Gap)> FirstRowsAsync(ILocator list)
    {
        await Expect(list.Locator("[data-row='1']")).ToBeVisibleAsync();
        var rows = await Page.WaitForFunctionAsync(@"el => {
            const a = el.querySelector('[data-row=""0""]'), b = el.querySelector('[data-row=""1""]');
            if (!a || !b) return null;
            const first = a.getBoundingClientRect(), second = b.getBoundingClientRect();
            return [first.height, second.top - first.bottom];
        }", await list.ElementHandleAsync());
        var m = await rows.JsonValueAsync<double[]>();
        return (m[0], m[1]);
    }

    /// <summary>Whether <paramref name="row"/> lies wholly inside <paramref name="list"/>'s box, to the pixel.</summary>
    private async Task ExpectInsideAsync(ILocator row, ILocator list, float topInset = 0)
    {
        await Expect(row).ToBeInViewportAsync();
        var outer = await BoxAsync(list);
        var inner = await BoxAsync(row);
        Assert.That(inner.Top, Is.GreaterThanOrEqualTo(outer.Top + topInset - 1));
        Assert.That(inner.Bottom, Is.LessThanOrEqualTo(outer.Bottom + 1));
    }

    private static async Task<(float Top, float Bottom, float Left, float Right)> BoxAsync(ILocator locator)
    {
        var b = await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("Element has no bounding box");
        return (b.Y, b.Y + b.Height, b.X, b.X + b.Width);
    }

    // ── Filtering and selection ───────────────────────────────────────────────

    [Test]
    public async Task Typing_FiltersAndEnterSelects()
    {
        await GoToPage();

        await Input("simple-combo").ClickAsync();
        await Input("simple-combo").PressSequentiallyAsync("new");

        await Expect(Options("simple-combo")).ToHaveTextAsync(new[] { "New Hampshire", "New Jersey", "New York" });
        await ExpectActiveAsync("simple-combo", "New Hampshire");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#simple-value")).ToHaveTextAsync("Value: New Jersey");
        await Expect(Input("simple-combo")).ToHaveValueAsync("New Jersey");
        await Expect(Input("simple-combo")).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Test]
    public async Task Typing_FastIntoLargeList_LosesNoCharacters()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-large").CheckAsync();
        await Page.Locator("#pg-group").CheckAsync();
        await OpenAsync("playground-combo");

        await Input("playground-combo").PressSequentiallyAsync("kiewit northeast #77");

        await Expect(Input("playground-combo")).ToHaveValueAsync("kiewit northeast #77");
        await Expect(Options("playground-combo").First).ToHaveTextAsync("Kiewit Northeast #77");
    }

    [Test]
    public async Task Click_OptionSelects_AndUpdatesValueAndItem()
    {
        await GoToPage();

        await OpenAsync("value-combo");
        await Options("value-combo").Filter(new() { HasText = "Brookside Utilities" }).ClickAsync();

        await Expect(Input("value-combo")).ToHaveValueAsync("Brookside Utilities");
        await Expect(Page.Locator("#value-id")).ToHaveTextAsync("Value: 3");
        await Expect(Page.Locator("#value-item")).ToHaveTextAsync("City: Allentown");
    }

    [Test]
    public async Task ValueAndSelectedItem_SetFromCode_StayInStep()
    {
        await GoToPage();

        await Expect(Input("value-combo")).ToHaveValueAsync("Granite State Builders");

        await Page.Locator("#value-set-11").ClickAsync();
        await Expect(Input("value-combo")).ToHaveValueAsync("Kiewit Northeast");
        await Expect(Page.Locator("#value-item")).ToHaveTextAsync("City: Newburgh");

        await Page.Locator("#value-set-item").ClickAsync();
        await Expect(Input("value-combo")).ToHaveValueAsync("Cape Cod Marine Works");
        await Expect(Page.Locator("#value-id")).ToHaveTextAsync("Value: 4");

        await Page.Locator("#value-clear").ClickAsync();
        await Expect(Input("value-combo")).ToHaveValueAsync("");
        await Expect(Page.Locator("#value-id")).ToHaveTextAsync("Value: null");
    }

    [Test]
    public async Task Filters_ContainsStartsWithNoneCustom()
    {
        await GoToPage();

        await Input("filter-contains").FillAsync("con");
        await Expect(Options("filter-contains")).ToHaveCountAsync(4);
        await Page.Keyboard.PressAsync("Escape");

        await Input("filter-starts").FillAsync("c");
        await Expect(Options("filter-starts")).ToHaveTextAsync(new[] { "Cape Cod Marine Works", "Cascade Civil" });
        await Page.Keyboard.PressAsync("Escape");

        await Input("filter-none").FillAsync("pat");
        await Expect(Options("filter-none")).ToHaveCountAsync(20);
        await ExpectActiveAsync("filter-none", "Patriot Earthworks");
        await Page.Keyboard.PressAsync("Escape");

        await Input("filter-custom").FillAsync("kiew");
        await Expect(Options("filter-custom")).ToHaveTextAsync(new[] { "KIEW — Kiewit Northeast" });
    }

    [Test]
    public async Task Filter_UnderlinesMatchedText()
    {
        await GoToPage();

        await Input("filter-contains").FillAsync("paving");

        await Expect(Options("filter-contains").Locator("mark")).ToHaveTextAsync("Paving");
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Keyboard_ArrowsDoNotMoveCaret_AndEnterDoesNotSubmitForm()
    {
        await GoToPage();

        await Input("form-customer").ClickAsync();
        await Input("form-customer").PressSequentiallyAsync("ca");
        await Page.Keyboard.PressAsync("ArrowDown");
        var caret = await Input("form-customer").EvaluateAsync<int>("el => el.selectionStart");
        Assert.That(caret, Is.EqualTo(2), "ArrowDown moved the caret");

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Input("form-customer")).ToHaveValueAsync("Cascade Civil");
        await Expect(Page.Locator("#combo-result")).ToHaveTextAsync("Not created");
    }

    [Test]
    public async Task Keyboard_EscapeClosesAndRestores()
    {
        await GoToPage();

        await Input("value-combo").FillAsync("bro");
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Input("value-combo")).ToHaveValueAsync("Granite State Builders");
    }

    [Test]
    public async Task Keyboard_TabWithPartialText_SelectsHighlightedAndMovesOn()
    {
        await GoToPage();

        await Input("simple-combo").FillAsync("verm");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#simple-value")).ToHaveTextAsync("Value: Vermont");
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Input("simple-combo")).Not.ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_GroupHeadersAreSkipped()
    {
        await GoToPage();

        await OpenAsync("group-combo");

        await Expect(List("group-combo").Locator(".nx-combobox-group")).ToHaveCountAsync(3);
        await ExpectActiveAsync("group-combo", "Brookside Utilities");
        for (var i = 0; i < 5; i++)
            await Page.Keyboard.PressAsync("ArrowDown");
        // Brookside → Delaware → Keystone → Pine Barrens → Susquehanna → (New England header skipped) → Acme.
        await ExpectActiveAsync("group-combo", "Acme Site Development");
    }

    // ── Closing ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ClickOutside_Closes()
    {
        await GoToPage();

        await OpenAsync("simple-combo");
        await Page.Locator("h1").ClickAsync();

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Input("simple-combo")).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Test]
    public async Task ToggleButton_OpensAndCloses_KeepingFocusInInput()
    {
        await GoToPage();

        var toggle = Field("simple-combo").Locator(".nx-combobox-toggle");
        await toggle.ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Input("simple-combo")).ToBeFocusedAsync();

        await toggle.ClickAsync();
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ScrollingThePopup_DoesNotCloseIt()
    {
        await GoToPage();

        await OpenAsync("filter-contains");
        await List("filter-contains").HoverAsync();
        await Page.Mouse.WheelAsync(0, 200);
        await Page.WaitForTimeoutAsync(200);

        await Expect(Popup).ToBeVisibleAsync();
        Assert.That(await List("filter-contains").EvaluateAsync<double>("el => el.scrollTop"), Is.GreaterThan(0));
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task ClearButton_Clears()
    {
        await GoToPage();

        await Field("value-combo").Locator(".nx-combobox-clear").ClickAsync();

        await Expect(Input("value-combo")).ToHaveValueAsync("");
        await Expect(Page.Locator("#value-id")).ToHaveTextAsync("Value: null");
        await Expect(Input("value-combo")).ToBeFocusedAsync();
    }

    // ── Data provider ─────────────────────────────────────────────────────────

    [Test]
    public async Task DataProvider_ShowsLoadingThenResults()
    {
        await GoToPage();

        await Input("provider-combo").ClickAsync();
        await Input("provider-combo").PressSequentiallyAsync("lake");

        await Expect(Input("provider-combo")).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(Options("provider-combo")).ToHaveCountAsync(1, new() { Timeout = 5000 });
        await Expect(Input("provider-combo")).Not.ToHaveAttributeAsync("aria-busy", "true");
        await Expect(Options("provider-combo").First).ToContainTextAsync("Lakeshore Excavation");

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#provider-value")).ToHaveTextAsync("Value: 12");
    }

    [Test]
    public async Task DataProvider_DebouncesTyping()
    {
        await GoToPage();

        await Input("provider-combo").ClickAsync();
        await Input("provider-combo").PressSequentiallyAsync("hud", new() { Delay = 40 });
        await Expect(Options("provider-combo")).ToHaveCountAsync(1, new() { Timeout = 5000 });

        var log = Section("Async Data Provider").Locator(".demo-log");
        await Expect(log.Locator(".demo-log-entry").Filter(new() { HasText = "started" })).ToHaveCountAsync(1);
        await Expect(log).ToContainTextAsync("DataProvider(\"hud\")");
    }

    // ── Virtualization ────────────────────────────────────────────────────────

    [Test]
    public async Task Virtualize_RendersAWindowOf50000_AndPageDownScrolls()
    {
        await GoToPage();

        await OpenAsync("virtual-combo");
        // Virtualize renders its rows a cycle after the popup appears; CountAsync does not retry.
        await Expect(Options("virtual-combo").First).ToBeVisibleAsync();
        var rendered = await Options("virtual-combo").CountAsync();
        Assert.That(rendered, Is.InRange(5, 100), "virtualization did not limit the rendered rows");
        await Expect(Options("virtual-combo").First).ToHaveAttributeAsync("aria-setsize", "50000");

        for (var i = 0; i < 5; i++)
            await Page.Keyboard.PressAsync("PageDown");

        var active = List("virtual-combo").Locator(".nx-combobox-item-active");
        await Expect(active).ToBeVisibleAsync();
        await Expect(active).ToHaveAttributeAsync("aria-posinset", "51");
        await Expect(active).ToBeInViewportAsync();

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#virtual-value")).ToHaveTextAsync("Value: 51");
    }

    [Test]
    public async Task Virtualize_RowsKeepTheirMeasuredHeight_SameAsAPlainList()
    {
        await GoToPage();

        await OpenAsync("simple-combo");
        var plain = await BoxAsync(Options("simple-combo").First);
        await Page.Keyboard.PressAsync("Escape");

        await OpenAsync("virtual-combo");
        var (height, gap) = await FirstRowsAsync(List("virtual-combo"));
        Assert.That(height, Is.EqualTo(plain.Bottom - plain.Top).Within(0.5));
        Assert.That(gap, Is.EqualTo(0).Within(0.5), "rows are stacked without gaps");
        await Expect(List("virtual-combo")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-combobox-list-measuring"));
    }

    [Test]
    public async Task Virtualize_TallTemplateRows_AreMeasured_AndKeyboardScrollStaysRight()
    {
        await GoToPage();

        await OpenAsync("virtual-template");
        await Expect(Options("virtual-template").First).ToBeVisibleAsync();
        Assert.That(await Options("virtual-template").CountAsync(), Is.InRange(3, 100), "virtualization did not limit the rendered rows");
        var (height, gap) = await FirstRowsAsync(List("virtual-template"));
        Assert.That(height, Is.GreaterThan(36), "two-line rows are taller than the default row");
        Assert.That(gap, Is.EqualTo(0).Within(0.5));
        // No row's content is clipped by the pinned height.
        Assert.That(await Options("virtual-template").First.EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight + 1"), Is.True);

        // Far down the list: the row arithmetic uses the measured height, so the active row is rendered and in view.
        var active = List("virtual-template").Locator(".nx-combobox-item-active");
        for (var i = 0; i < 30; i++)
            await Page.Keyboard.PressAsync("PageDown");
        await Expect(active).ToHaveAttributeAsync("aria-posinset", "301");
        await ExpectInsideAsync(active, List("virtual-template"));

        for (var i = 0; i < 3; i++)
            await Page.Keyboard.PressAsync("PageUp");
        await Expect(active).ToHaveAttributeAsync("aria-posinset", "271");
        await ExpectInsideAsync(active, List("virtual-template"));

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#virtual-template-value")).ToHaveTextAsync("Value: 271");
    }

    [Test]
    public async Task Virtualize_ReopenedOnASelectionFarDown_ShowsIt()
    {
        await GoToPage();

        await Input("virtual-template").FillAsync("P-40000");
        await Expect(Options("virtual-template")).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#virtual-template-value")).ToHaveTextAsync("Value: 40000");

        await OpenAsync("virtual-template");
        var active = List("virtual-template").Locator(".nx-combobox-item-active");
        await Expect(active).ToHaveAttributeAsync("aria-posinset", "40000");
        await ExpectInsideAsync(active, List("virtual-template"));
    }

    [Test]
    public async Task ItemByValue_ShowsTheNameBeforeTheListLoads()
    {
        await GoToPage();

        await Expect(Input("lookup-combo")).ToHaveValueAsync("Lakeshore Excavation");
        await Expect(Input("lookup-none")).ToHaveValueAsync("");
        await Expect(Page.Locator("#lookup-value")).ToHaveTextAsync("Value: 12");

        // The looked-up item is the selection: it is the highlighted row once the list loads.
        await OpenAsync("lookup-combo");
        await ExpectActiveAsync("lookup-combo", "Lakeshore Excavation");
        await Options("lookup-combo").Filter(new() { HasText = "Brookside Utilities" }).ClickAsync();
        await Expect(Page.Locator("#lookup-value")).ToHaveTextAsync("Value: 3");
        await Expect(Input("lookup-combo")).ToHaveValueAsync("Brookside Utilities");
    }

    [Test]
    public async Task Virtualize_FilterNarrowsLargeList()
    {
        await GoToPage();

        await Input("virtual-combo").FillAsync("P-49999");

        await Expect(Options("virtual-combo")).ToHaveCountAsync(1);
    }

    // ── Templates ─────────────────────────────────────────────────────────────

    [Test]
    public async Task SelectedItemTemplate_ShownUntilFocused()
    {
        await GoToPage();

        var selected = Combo("template-combo").Locator(".nx-combobox-selected");
        await Expect(selected).ToContainTextAsync("Priya Raman");
        await Expect(selected.Locator(".demo-avatar")).ToHaveTextAsync("PR");

        await Input("template-combo").FocusAsync();
        await Expect(selected).ToHaveCountAsync(0);
        await Expect(Input("template-combo")).ToHaveValueAsync("Priya Raman");
    }

    [Test]
    public async Task ItemTemplate_RendersAvatarBesideName()
    {
        await GoToPage();

        await OpenAsync("template-combo");
        var first = Options("template-combo").First;
        var avatar = await BoxAsync(first.Locator(".demo-avatar"));
        var name = await BoxAsync(first.Locator(".demo-item-main"));

        Assert.That(name.Left, Is.GreaterThan(avatar.Right - 1), "avatar is not beside the name");
        Assert.That(name.Top, Is.LessThan(avatar.Bottom), "avatar and name are not on one row");
    }

    // ── Custom values ─────────────────────────────────────────────────────────

    [Test]
    public async Task CustomValue_KeptOnEnter()
    {
        await GoToPage();

        await Input("custom-combo").FillAsync("Barge");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#custom-value")).ToHaveTextAsync("Value: Barge");
        await Expect(Input("custom-combo")).ToHaveValueAsync("Barge");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public async Task Validation_EmptySubmit_ThenFix()
    {
        await GoToPage();

        await Page.Locator("#combo-submit").ClickAsync();
        var messages = Page.Locator("#combo-form .validation-message");
        await Expect(messages).ToHaveTextAsync(new[] { "Pick a customer.", "Pick the job's state." });
        await Expect(Input("form-customer")).ToHaveAttributeAsync("aria-invalid", "true");

        await Input("form-customer").FillAsync("kiew");
        await Page.Keyboard.PressAsync("Enter");
        await Input("form-state").FillAsync("vermont");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Input("form-customer")).Not.ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(messages).ToHaveCountAsync(0);

        await Page.Locator("#combo-submit").ClickAsync();
        await Expect(Page.Locator("#combo-result")).ToHaveTextAsync("Created for Kiewit Northeast, Vermont");
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    /// <summary>Scrolls the page so the field's bottom edge sits <paramref name="gap"/> px above the window's bottom.</summary>
    /// <summary>
    /// Scrolls the page so the field's top edge sits <paramref name="top"/> px below the window's top, and fails
    /// the test setup if the page is too short to get it there (the popup would then rightly flip).
    /// </summary>
    private async Task PlaceFieldTopAsync(string id, int top)
    {
        await Field(id).EvaluateAsync("(el, top) => window.scrollBy(0, el.getBoundingClientRect().top - top)", top);
        var placed = (await Field(id).BoundingBoxAsync())!.Y;
        Assert.That(placed, Is.EqualTo(top).Within(2), "test setup: the page cannot scroll the field into place");
    }

    private Task PlaceFieldBottomAsync(string id, int gap) =>
        Field(id).EvaluateAsync("(el, gap) => window.scrollBy(0, el.getBoundingClientRect().bottom - (innerHeight - gap))", gap);

    [Test]
    public async Task Popup_OpensBelowField()
    {
        await GoToPage();
        await PlaceFieldTopAsync("position-combo", 150);

        var field = await BoxAsync(Field("position-combo"));
        await OpenAsync("position-combo");

        var popup = await BoxAsync(Popup);
        await Expect(Popup).ToHaveAttributeAsync("data-placement", "below");
        Assert.That(popup.Top, Is.EqualTo(field.Bottom + 4).Within(1.5), "popup does not hang from the field");
        Assert.That(popup.Left, Is.EqualTo(field.Left).Within(1.5));
    }

    [Test]
    public async Task Popup_FlipsAboveNearBottomEdge_AndKeepsBottomEdgeWhileFiltering()
    {
        await GoToPage();
        await PlaceFieldBottomAsync("position-combo", gap: 10);

        var field = await BoxAsync(Field("position-combo"));
        await OpenAsync("position-combo");

        await Expect(Popup).ToHaveAttributeAsync("data-placement", "above");
        var opened = await BoxAsync(Popup);
        Assert.That(opened.Bottom, Is.EqualTo(field.Top - 4).Within(1.5), "popup does not sit on top of the field");
        Assert.That(opened.Top, Is.GreaterThanOrEqualTo(0), "popup ran off the top of the window");

        await Input("position-combo").PressSequentiallyAsync("new");
        await Expect(Options("position-combo")).ToHaveCountAsync(3);

        var filtered = await BoxAsync(Popup);
        Assert.That(filtered.Bottom - filtered.Top, Is.LessThan(opened.Bottom - opened.Top), "test setup: filtering did not shrink the list");
        Assert.That(filtered.Bottom, Is.EqualTo(field.Top - 4).Within(1.5), "filtered popup detached from the field");
    }

    [Test]
    public async Task Popup_EscapesOverflowHiddenContainer()
    {
        await GoToPage();
        await PlaceFieldTopAsync("clip-combo", 150);

        await OpenAsync("clip-combo");

        var clip = await BoxAsync(Page.Locator(".demo-clip-box"));
        var popup = await BoxAsync(Popup);
        Assert.That(popup.Bottom, Is.GreaterThan(clip.Bottom + 100), "test setup: the popup does not extend past the box");

        // The lowest visible option is really on top, not clipped away.
        var lastVisible = Options("clip-combo").Nth(5);
        await lastVisible.ClickAsync();
        await Expect(Input("clip-combo")).ToHaveValueAsync("New Hampshire");
    }

    [Test]
    public async Task Popup_FollowsFieldWhenPageScrolls()
    {
        await GoToPage();
        await PlaceFieldTopAsync("position-combo", 150);
        await OpenAsync("position-combo");
        await Expect(Popup).ToHaveAttributeAsync("data-placement", "below");

        await Page.EvaluateAsync("window.scrollBy(0, 60)");
        await Page.WaitForTimeoutAsync(150);

        var field = await BoxAsync(Field("position-combo"));
        var popup = await BoxAsync(Popup);
        Assert.That(popup.Top, Is.EqualTo(field.Bottom + 4).Within(1.5));
    }

    // ── Playground ────────────────────────────────────────────────────────────

    [Test]
    public async Task Playground_ReadOnlyAndDisabled_DoNotOpen()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-readonly").CheckAsync();
        await Input("playground-combo").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Input("playground-combo")).ToHaveAttributeAsync("aria-expanded", "false");

        await Page.Locator("#pg-readonly").UncheckAsync();
        await Page.Locator("#pg-disabled").CheckAsync();
        await Expect(Input("playground-combo")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Playground_LoadingShowsSpinner()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-loading").CheckAsync();

        await Expect(Field("playground-combo").Locator(".nx-combobox-spinner")).ToBeVisibleAsync();
        await Expect(Input("playground-combo")).ToHaveAttributeAsync("aria-busy", "true");
    }

    [Test]
    public async Task Playground_ProviderSource_LoadsFromServer()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-source-provider").ClickAsync();
        await Input("playground-combo").ClickAsync();
        await Input("playground-combo").PressSequentiallyAsync("cape");

        await Expect(Options("playground-combo")).ToHaveCountAsync(1, new() { Timeout = 5000 });
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: \"CAPE\"");
        await Expect(Page.Locator("#playground-item")).ToHaveTextAsync("SelectedItem: Cape Cod Marine Works");
    }

    // ── Accessibility ─────────────────────────────────────────────────────────

    [Test]
    public async Task Accessibility_RolesAndActiveDescendant()
    {
        await GoToPage();

        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "State", Exact = true })).ToHaveCountAsync(1);
        await OpenAsync("simple-combo");

        await Expect(Page.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(1);
        // Scoped to the list: the playground's native <select> options also have role=option.
        await Expect(List("simple-combo").GetByRole(AriaRole.Option)).ToHaveCountAsync(12);
        var active = await Input("simple-combo").GetAttributeAsync("aria-activedescendant");
        await Expect(Page.Locator($"#{active}")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-combobox-item-active"));
        await Expect(Input("simple-combo")).ToBeFocusedAsync();
    }

    // ── Multi-column ──────────────────────────────────────────────────────────

    [Test]
    public async Task Columns_HeaderAlignsWithCells_AndStaysOnScroll()
    {
        await GoToPage();

        await OpenAsync("columns-combo");
        var header = List("columns-combo").Locator(".nx-combobox-columns-header");
        await Expect(header).ToBeVisibleAsync();
        await Expect(header.Locator(".nx-combobox-cell")).ToHaveTextAsync(new[] { "Code", "Customer", "City", "Region" });

        var headerCity = await BoxAsync(header.Locator(".nx-combobox-cell").Nth(2));
        var cellCity = await BoxAsync(Options("columns-combo").First.Locator(".nx-combobox-cell").Nth(2));
        Assert.That(cellCity.Left, Is.EqualTo(headerCity.Left).Within(1), "City cells do not line up with their header");

        await List("columns-combo").EvaluateAsync("el => el.scrollTop = 200");
        await Page.WaitForTimeoutAsync(100);
        var list = await BoxAsync(List("columns-combo"));
        var stuck = await BoxAsync(header);
        Assert.That(stuck.Top, Is.EqualTo(list.Top).Within(1.5), "the header scrolled away with the rows");
    }

    [Test]
    public async Task Columns_FilterMatchesCityColumn_FieldShowsName()
    {
        await GoToPage();

        await Input("columns-combo").FillAsync("hyannis");
        await Expect(Options("columns-combo")).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Input("columns-combo")).ToHaveValueAsync("Cape Cod Marine Works");
        await Expect(Page.Locator("#columns-value")).ToHaveTextAsync("Value: 4");
    }

    [Test]
    public async Task Columns_Virtualized50000_KeyboardStillWorks()
    {
        await GoToPage();

        await OpenAsync("columns-parts");
        await Expect(Options("columns-parts").First).ToBeVisibleAsync();
        Assert.That(await Options("columns-parts").CountAsync(), Is.InRange(5, 100));

        for (var i = 0; i < 3; i++)
            await Page.Keyboard.PressAsync("PageDown");
        var active = List("columns-parts").Locator(".nx-combobox-item-active");
        await Expect(active).ToHaveAttributeAsync("aria-posinset", "31");
        await Expect(active).ToBeInViewportAsync();

        // The active row is not hidden under the sticky header.
        var header = await BoxAsync(List("columns-parts").Locator(".nx-combobox-columns-header"));
        var row = await BoxAsync(active);
        Assert.That(row.Top, Is.GreaterThanOrEqualTo(header.Bottom - 1));
    }

    // ── Multi-select ──────────────────────────────────────────────────────────

    private ILocator Chips(string id) => Combo(id).Locator(".nx-combobox-chip:not(.nx-combobox-chip-more)");

    [Test]
    public async Task Multiple_ClickTogglesAndKeepsListOpen()
    {
        await GoToPage();

        await Expect(Chips("multi-combo")).ToHaveTextAsync(new[] { "MBMarcus Bell", "TKTom Kowalski" });
        await OpenAsync("multi-combo");
        await Options("multi-combo").Filter(new() { HasText = "Dana Ortiz" }).ClickAsync();

        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Page.Locator("#multi-values")).ToHaveTextAsync("Values: [2, 4, 1]");
        await Expect(Options("multi-combo").Filter(new() { HasText = "Dana Ortiz" })).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(List("multi-combo")).ToHaveAttributeAsync("aria-multiselectable", "true");
    }

    [Test]
    public async Task Multiple_KeyboardEnterSpaceBackspace()
    {
        await GoToPage();

        await OpenAsync("multi-combo");                      // highlight starts on the first chip, Marcus Bell
        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync("Enter");             // Dana Ortiz on
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync(" ");                 // Priya Raman on
        await Expect(Page.Locator("#multi-values")).ToHaveTextAsync("Values: [2, 4, 1, 3]");
        await Expect(Input("multi-combo")).ToHaveValueAsync("");
        await Expect(Popup).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Page.Keyboard.PressAsync("Backspace");
        await Expect(Page.Locator("#multi-values")).ToHaveTextAsync("Values: [2, 4, 1]");
    }

    [Test]
    public async Task Multiple_SpaceTypesWhileFiltering()
    {
        await GoToPage();

        await Input("multi-combo").ClickAsync();
        await Input("multi-combo").PressSequentiallyAsync("sam w");

        await Expect(Input("multi-combo")).ToHaveValueAsync("sam w");
        await Expect(Options("multi-combo")).ToHaveTextAsync(new[] { "Sam Whitaker" });
    }

    [Test]
    public async Task Multiple_MaxVisibleChips_ShowsMore()
    {
        await GoToPage();

        await OpenAsync("multi-combo");
        await Options("multi-combo").Nth(0).ClickAsync();
        await Options("multi-combo").Nth(2).ClickAsync();

        await Expect(Chips("multi-combo")).ToHaveCountAsync(3);
        await Expect(Combo("multi-combo").Locator(".nx-combobox-chip-more")).ToHaveTextAsync("+1 more");
    }

    [Test]
    public async Task Multiple_ChipRemoveAndClearAll()
    {
        await GoToPage();

        await Combo("multi-combo").GetByRole(AriaRole.Button, new() { Name = "Remove Marcus Bell" }).ClickAsync();
        await Expect(Page.Locator("#multi-values")).ToHaveTextAsync("Values: [4]");

        await Field("multi-combo").Locator(".nx-combobox-clear").ClickAsync();
        await Expect(Page.Locator("#multi-values")).ToHaveTextAsync("Values: []");
        await Expect(Chips("multi-combo")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Multiple_WithColumns_BindsSelectedItems()
    {
        await GoToPage();

        await OpenAsync("multi-states");
        await Options("multi-states").Nth(0).ClickAsync();
        await Options("multi-states").Nth(1).ClickAsync();

        await Expect(Page.Locator("#multi-items")).ToHaveTextAsync("SelectedItems: 2");
        await Expect(List("multi-states").Locator(".nx-combobox-check")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Multiple_Validation_OnCollection()
    {
        await GoToPage();

        await Page.Locator("#multi-submit").ClickAsync();
        await Expect(Page.Locator("#multi-form .validation-message")).ToHaveTextAsync("Pick at least two inspectors.");

        await OpenAsync("multi-required");
        await Options("multi-required").Nth(0).ClickAsync();
        await Options("multi-required").Nth(1).ClickAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator("#multi-form .validation-message")).ToHaveCountAsync(0);
        await Page.Locator("#multi-submit").ClickAsync();
        await Expect(Page.Locator("#multi-result")).ToHaveTextAsync("Assigned Dana Ortiz, Marcus Bell");
    }

    // ── Mobile overlay ────────────────────────────────────────────────────────

    private ILocator Overlay => Page.Locator(".nx-combobox-overlay");

    [Test]
    public async Task Mobile_ForcedOnDesktop_OpensFullScreenWithFocusedSearch()
    {
        await GoToPage();

        await Field("mobile-combo").ClickAsync();

        await Expect(Overlay).ToBeVisibleAsync();
        await Expect(Overlay).ToHaveAttributeAsync("role", "dialog");
        var box = await BoxAsync(Overlay);
        var area = await FixedArea.SizeAsync(Page);
        Assert.That(box.Right - box.Left, Is.GreaterThanOrEqualTo(area.Width - 20));
        Assert.That(box.Bottom - box.Top, Is.EqualTo(area.Height).Within(1));
        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToBeFocusedAsync();
        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToHaveAttributeAsync("placeholder", "Find a customer");
    }

    [Test]
    public async Task Mobile_SearchPickSetsValue_BackAndEscapeDoNot()
    {
        await GoToPage();

        await Field("mobile-combo").ClickAsync();
        // Focus moves to the search box after the overlay renders; typing before that goes nowhere.
        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("pine");
        await Expect(Overlay.Locator("[role=option]")).ToHaveCountAsync(1);
        await Overlay.Locator("[role=option]").ClickAsync();
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(Page.Locator("#mobile-value")).ToHaveTextAsync("Value: 16");

        await Field("mobile-combo").ClickAsync();
        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("acme");
        await Overlay.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(Page.Locator("#mobile-value")).ToHaveTextAsync("Value: 16");

        await Field("mobile-combo").ClickAsync();
        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(Page.Locator("#mobile-value")).ToHaveTextAsync("Value: 16");
    }

    [Test]
    public async Task Mobile_MultipleWithColumns_DoneCommits_BackDiscards()
    {
        await GoToPage();

        await Field("mobile-multi").ClickAsync();
        await Expect(Overlay.Locator(".nx-combobox-columns-header")).ToBeVisibleAsync();
        await Overlay.Locator("[role=option]").Nth(0).ClickAsync();
        await Overlay.Locator("[role=option]").Nth(1).ClickAsync();
        await Expect(Page.Locator("#mobile-multi-values")).ToHaveTextAsync("Values: []");
        await Overlay.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        await Expect(Page.Locator("#mobile-multi-values")).ToHaveTextAsync("Values: [1, 2]");

        await Field("mobile-multi").ClickAsync();
        await Overlay.Locator("[role=option]").Nth(2).ClickAsync();
        await Overlay.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Expect(Page.Locator("#mobile-multi-values")).ToHaveTextAsync("Values: [1, 2]");
    }

    [Test]
    public async Task Mobile_VirtualizedOverlay_RowsKeepTheTouchHeight()
    {
        await GoToPage();

        await Field("mobile-multi").ClickAsync();
        await Expect(Overlay.Locator("[role=option]").First).ToBeVisibleAsync();
        Assert.That(await Overlay.Locator("[role=option]").CountAsync(), Is.LessThan(100), "virtualization did not limit the rendered rows");
        var (height, gap) = await FirstRowsAsync(Overlay.Locator(".nx-combobox-overlay-list"));
        Assert.That(height, Is.GreaterThanOrEqualTo(44));
        Assert.That(gap, Is.EqualTo(0).Within(0.5));

        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToBeFocusedAsync();
        for (var i = 0; i < 20; i++)
            await Page.Keyboard.PressAsync("PageDown");
        var active = Overlay.Locator(".nx-combobox-item-active");
        await Expect(active).ToHaveAttributeAsync("aria-posinset", "201");
        var header = await BoxAsync(Overlay.Locator(".nx-combobox-columns-header"));
        await ExpectInsideAsync(active, Overlay.Locator(".nx-combobox-overlay-list"), topInset: header.Bottom - header.Top);
    }

    [Test]
    public async Task Mobile_PhoneViewport_DefaultIsThePopup()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await Page.GotoAsync(_baseUrl + "/combobox/examples");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await Input("simple-combo").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(Input("simple-combo")).Not.ToHaveAttributeAsync("readonly", "");
    }

    [Test]
    public async Task Mobile_PhoneViewport_AutoExampleUsesOverlay()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await Page.GotoAsync(_baseUrl + "/combobox/examples");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        await Expect(Input("mobile-auto")).ToHaveAttributeAsync("readonly", "");

        await Field("mobile-auto").ClickAsync();
        await Expect(Overlay).ToBeVisibleAsync();
        await Overlay.GetByRole(AriaRole.Option, new() { Name = "Vermont", Exact = true }).ClickAsync();

        await Expect(Page.Locator("#mobile-auto-value")).ToHaveTextAsync("Value: Vermont");
    }

    [Test]
    public async Task Mobile_PhoneViewport_AutoModeUsesOverlay_AndLocksPageScroll()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await Page.GotoAsync(_baseUrl + "/combobox/playground");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        await Expect(Input("playground-combo")).Not.ToHaveAttributeAsync("readonly", "");
        await Page.Locator("#pg-mobile").SelectOptionAsync("Auto");
        await Expect(Input("playground-combo")).ToHaveAttributeAsync("readonly", "");

        await Field("playground-combo").ClickAsync();

        await Expect(Overlay).ToBeVisibleAsync();
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.True);
        await Expect(Overlay.Locator(".nx-combobox-overlay-search")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("lake");
        await Overlay.Locator("[role=option]").First.ClickAsync();

        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: \"LAKE\"");
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.False);
    }

    [Test]
    public async Task Playground_MobileModeToggle()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-mobile").SelectOptionAsync("Always");
        await Field("playground-combo").ClickAsync();
        await Expect(Overlay).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Page.Locator("#pg-mobile").SelectOptionAsync("Never");
        await Field("playground-combo").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Overlay).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Playground_MultipleAndColumnsToggles()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-multiple").CheckAsync();
        await Page.Locator("#pg-columns").CheckAsync();
        await OpenAsync("playground-combo");

        await Expect(List("playground-combo").Locator(".nx-combobox-columns-header")).ToBeVisibleAsync();
        await Options("playground-combo").Nth(0).ClickAsync();
        await Options("playground-combo").Nth(1).ClickAsync();
        await Expect(Page.Locator("#playground-values")).ToHaveTextAsync("Values: [ACME, BALD]");
    }
}
