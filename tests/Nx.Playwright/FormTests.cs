using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class FormTests : PageTest
{
    private readonly string _baseUrl;

    public FormTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/form/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private static async Task<float> X(ILocator l) => (await l.BoundingBoxAsync())!.X;
    private static async Task<float> Y(ILocator l) => (await l.BoundingBoxAsync())!.Y;

    [Test]
    public async Task GeneratedEditors_BindTheModel()
    {
        await GoToPage();

        var trucks = Page.Locator("#generated-form-Trucks");
        await trucks.FillAsync("7");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#generated-model")).ToContainTextAsync("Trucks    = 7");

        await Page.Locator("#generated-form-Status").ClickAsync();
        await Page.Locator(".nx-combobox-popup .nx-combobox-item", new() { HasText = "On hold" }).ClickAsync();
        await Expect(Page.Locator("#generated-model")).ToContainTextAsync("Status    = OnHold");

        await Page.Locator(".nx-form-item:has(#generated-form-PumpTruck) .nx-checkbox").ClickAsync();
        await Expect(Page.Locator("#generated-model")).ToContainTextAsync("PumpTruck = False");
    }

    [Test]
    public async Task Label_ClickFocusesItsEditor()
    {
        await GoToPage();

        await Page.Locator("#generated-form .nx-form-label", new() { HasText = "Volume" }).ClickAsync();
        await Expect(Page.Locator("#generated-form-Volume")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Columns_ThirdsAndHalves_OnADesktop()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage();

        var first = Page.Locator("#columns-form-FirstName");
        var middle = Page.Locator("#columns-form-MiddleName");
        var last = Page.Locator("#columns-form-LastName");
        var title = Page.Locator("#columns-form-Title");
        Assert.That(await Y(middle), Is.EqualTo(await Y(first)).Within(1));
        Assert.That(await Y(last), Is.EqualTo(await Y(first)).Within(1));
        Assert.That(await X(middle), Is.GreaterThan(await X(first)));
        Assert.That(await Y(title), Is.GreaterThan(await Y(first)));

        var firstBox = (await first.BoundingBoxAsync())!;
        var titleBox = (await title.BoundingBoxAsync())!;
        Assert.That(titleBox.Width / firstBox.Width, Is.EqualTo(1.5).Within(0.1));
    }

    [Test]
    public async Task Phone_EveryItemTakesTheRow_LabelsAbove()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/form/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var first = (await page.Locator("#columns-form-FirstName").BoundingBoxAsync())!;
        var middle = (await page.Locator("#columns-form-MiddleName").BoundingBoxAsync())!;
        Assert.That(middle.Y, Is.GreaterThan(first.Y + first.Height));
        Assert.That(middle.Width, Is.EqualTo(first.Width).Within(1));

        // A horizontal form puts its labels back above the editors.
        var label = (await page.Locator("#horizontal-form .nx-form-label", new() { HasText = "Name" }).BoundingBoxAsync())!;
        var name = (await page.Locator("#horizontal-form-Name").BoundingBoxAsync())!;
        Assert.That(label.Y + label.Height, Is.LessThanOrEqualTo(name.Y + 1));

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    [Test]
    public async Task NoEmptySpaceUnderAFormInAFlexColumn()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage();

        var heights = await Page.EvaluateAsync<double[]>(
            "() => { const f = document.querySelector('#columns-form'); return [f.getBoundingClientRect().height, f.querySelector('.nx-form-body').getBoundingClientRect().height]; }");
        Assert.That(heights[0], Is.EqualTo(heights[1]).Within(1));
    }

    [Test]
    public async Task Horizontal_LabelsBesideEditors_RightAligned()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage();

        var label = (await Page.Locator("#horizontal-form .nx-form-label", new() { HasText = "Pour date" }).BoundingBoxAsync())!;
        var editor = (await Page.Locator(".nx-field:has(#horizontal-form-PourDate) .nx-field-box").BoundingBoxAsync())!;
        Assert.That(label.X + label.Width, Is.LessThan(editor.X));
        Assert.That(label.Y + label.Height / 2, Is.EqualTo(editor.Y + editor.Height / 2).Within(3));
    }

    [Test]
    public async Task Submit_ShowsMessages_FixingOneClearsIt()
    {
        await GoToPage();

        await Page.Locator("#validation-submit").ClickAsync();
        await Expect(Page.Locator("#validation-result")).ToHaveTextAsync("Fix the marked fields");
        await Expect(Page.Locator("#validation-form .nx-validation-message")).ToHaveCountAsync(2);
        await Expect(Page.Locator("#validation-form-FirstName")).ToHaveAttributeAsync("aria-invalid", "true");

        await Page.Locator("#validation-form-FirstName").FillAsync("Dana");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#validation-form .nx-validation-message")).ToHaveCountAsync(1);

        await Page.Locator("#validation-form-LastName").FillAsync("Ortiz");
        await Page.Locator("#validation-form-Email").FillAsync("not-an-email");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator(".nx-form-item:has(#validation-form-Email) .nx-validation-message")).ToHaveTextAsync("Not an email address.");

        await Page.Locator("#validation-form-Email").FillAsync("dana@example.com");
        await Page.Locator("#validation-submit").ClickAsync();
        await Expect(Page.Locator("#validation-result")).ToHaveTextAsync("Saved");
    }

    [Test]
    public async Task SubmitOnEnterFalse_EnterDoesNotSubmit()
    {
        await GoToPage();

        await Page.Locator("#validation-form-FirstName").FillAsync("Dana");
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(300);
        await Expect(Page.Locator("#validation-result")).ToHaveTextAsync("Not submitted");
        await Expect(Page.Locator("#validation-form .nx-validation-message")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SubmitOnEnterTrue_EnterSubmits()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-form-Name").FillAsync("Deck pour");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-result")).ToHaveTextAsync("Saved “Deck pour”");
    }

    [Test]
    public async Task IsValid_FromOutsideTheSubmit()
    {
        await GoToPage();

        await Page.Locator("#validation-check").ClickAsync();
        await Expect(Page.Locator("#validation-result")).ToHaveTextAsync("IsValid() = false");
        await Expect(Page.Locator("#validation-form .nx-validation-message")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task OtherValidator_ReplacesDataAnnotations()
    {
        await GoToPage();

        await Page.Locator("#reach-submit").ClickAsync();
        await Expect(Page.Locator("#custom-validator-form .nx-validation-message").First).ToHaveTextAsync("Give a phone or an email.");

        await Page.Locator("#custom-validator-form-Phone").FillAsync("555-0100");
        await Page.Locator("#reach-submit").ClickAsync();
        await Expect(Page.Locator("#reach-result")).ToHaveTextAsync("Saved");
    }

    [Test]
    public async Task OwnEditors_LabelTargetsThem()
    {
        await GoToPage();

        await Page.Locator("#custom-form .nx-form-label", new() { HasText = "Customer" }).ClickAsync();
        await Expect(Page.Locator("#custom-customer")).ToBeFocusedAsync();
        await Expect(Page.Locator("#custom-form .nx-form-label", new() { HasText = "PO number" })).ToHaveAttributeAsync("for", "custom-po");
    }

    [Test]
    public async Task ValidationMessage_Standalone()
    {
        await GoToPage();

        await Page.Locator("#message-submit").ClickAsync();
        await Expect(Page.Locator("#message-form .nx-validation-message")).ToHaveTextAsync("1 to 12 trucks.");
        await Expect(Page.Locator("#message-form .nx-validation")).ToHaveAttributeAsync("aria-live", "polite");
    }

    [Test]
    public async Task Playground_ColumnsAndOrientation()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("playground");

        await Page.Locator("#pg-columns").FillAsync("1");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#playground-form .nx-form-items")).ToHaveAttributeAsync("style", new Regex("--nx-form-columns: 1"));

        await Page.Locator("#pg-orientation").SelectOptionAsync("Horizontal");
        await Expect(Page.Locator("#playground-form")).ToHaveClassAsync(new Regex("nx-form-horizontal"));

        await Page.Locator("#pg-group").CheckAsync();
        await Expect(Page.Locator("#playground-form fieldset legend")).ToHaveTextAsync("Crew");
    }
}
