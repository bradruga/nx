using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>
/// The demo's code blocks (DocCode, DocExampleCode): every example shows its code, the code is highlighted in both
/// themes, and the copy button copies it.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DemoCodeTests : PageTest
{
    private readonly string _baseUrl;

    public DemoCodeTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Section(string title) =>
        Page.Locator(".doc-section").Filter(new() { Has = Page.Locator("h2", new() { HasTextString = title }) });

    // ── Every example has code ────────────────────────────────────────────────

    [Test]
    public async Task EveryExamplesSection_HasACodeBlockOrShowCode()
    {
        var missing = new List<string>();
        foreach (var slug in SiteLayoutTests.Components)
        {
            await GoToPage($"/{slug}/examples");
            var titles = await Page.EvaluateAsync<string[]>(
                """
                () => [...document.querySelectorAll('.doc-section')]
                    .filter(s => !s.querySelector('.doc-code-block, .doc-example-code-toggle'))
                    .map(s => s.querySelector('h2')?.textContent ?? '(no heading)')
                """);
            missing.AddRange(titles.Select(t => $"{slug}: {t}"));
        }

        Assert.That(missing, Is.Empty, "Examples sections without code");
    }

    // ── Show code ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ShowCode_RevealsTheExampleMarkup_WithoutDemoIdsOrClasses()
    {
        await GoToPage("/button/examples");
        var section = Section("Full Width");
        var toggle = section.Locator(".doc-example-code-toggle");

        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(section.Locator(".doc-code")).ToHaveCountAsync(0);

        await toggle.ClickAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(toggle).ToHaveTextAsync("Hide code");
        await Expect(section.Locator(".doc-code")).ToHaveTextAsync(
            "<NxButton Variant=\"NxButtonVariant.Primary\" FullWidth=\"true\" Size=\"NxButtonSize.Large\">Sign in</NxButton>\n" +
            "<NxButton FullWidth=\"true\">Create an account</NxButton>");

        await toggle.ClickAsync();
        await Expect(section.Locator(".doc-code")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ShowCode_KeepsIdsTheExamplePointsAt()
    {
        await GoToPage("/autocomplete/examples");
        var section = Section("Minimum Length");
        await section.Locator(".doc-example-code-toggle").ClickAsync();

        var code = section.Locator(".doc-code");
        await Expect(code).ToContainTextAsync("<label for=\"job-number\">");
        await Expect(code).ToContainTextAsync("<NxAutoComplete Id=\"job-number\"");
        await Expect(code).Not.ToContainTextAsync("demo-");
        await Expect(code).Not.ToContainTextAsync("@*");
    }

    // ── Highlighting ──────────────────────────────────────────────────────────

    [Test]
    public async Task Code_IsHighlighted_InLightAndDark()
    {
        await GoToPage("/button/examples");
        var code = Section("Variants").Locator(".doc-code");
        await Expect(code.Locator(".tok-tag").First).ToHaveTextAsync("<NxButton");
        await Expect(code.Locator(".tok-attr").First).ToHaveTextAsync("Variant");
        await Expect(code.Locator(".tok-str").First).ToHaveTextAsync("\"NxButtonVariant.Primary\"");

        var tag = code.Locator(".tok-tag").First;
        var light = await tag.EvaluateAsync<string>("e => getComputedStyle(e).color");
        var plain = await code.EvaluateAsync<string>("e => getComputedStyle(e).color");
        await Page.EvaluateAsync("() => document.documentElement.classList.add('nx-theme-dark')");
        var dark = await tag.EvaluateAsync<string>("e => getComputedStyle(e).color");

        Assert.That(light, Is.Not.EqualTo(plain), "tags are colored");
        Assert.That(dark, Is.Not.EqualTo(light), "dark theme has its own token colors");
    }

    [Test]
    public async Task Code_HighlightsRazorAndCSharp()
    {
        await GoToPage("/button/examples");
        var code = Section("Loading State").Locator(".doc-code");

        await Expect(code.Locator(".tok-razor", new() { HasTextString = "@code" })).ToHaveCountAsync(1);
        await Expect(code.Locator(".tok-kw", new() { HasTextString = "async" })).ToHaveCountAsync(1);
        await Expect(code.Locator(".tok-num", new() { HasTextString = "1500" })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task PlaygroundCode_IsHighlighted_AndFollowsTheControls()
    {
        await GoToPage("/button/playground");
        var code = Page.Locator(".demo-playground-code .doc-code");

        await Expect(code.Locator(".tok-tag").First).ToHaveTextAsync("<NxButton");
        await Expect(Page.Locator(".demo-playground-code .doc-code-copy")).ToBeVisibleAsync();
    }

    // ── Copy ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task CopyButton_CopiesTheCode_AndSaysCopied()
    {
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await GoToPage("/button/examples");
        var section = Section("Variants");
        var copy = section.Locator(".doc-code-copy");

        await Expect(copy).ToHaveAttributeAsync("aria-label", "Copy code");
        await copy.ClickAsync();
        await Expect(copy).ToHaveTextAsync("Copied");

        var copied = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        var shown = await section.Locator(".doc-code").EvaluateAsync<string>("e => e.textContent");
        Assert.That(copied, Is.EqualTo(shown));
        Assert.That(copied, Does.StartWith("<NxButton Variant=\"NxButtonVariant.Primary\">Primary</NxButton>"));

        await Expect(copy).ToHaveTextAsync("Copy", new() { Timeout = 4000 });
    }

    [Test]
    public async Task CopyButton_CopiesExampleCode()
    {
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await GoToPage("/button/examples");
        var section = Section("Full Width");
        await section.Locator(".doc-example-code-toggle").ClickAsync();
        await section.Locator(".doc-code-copy").ClickAsync();
        await Expect(section.Locator(".doc-code-copy")).ToHaveTextAsync("Copied");

        var copied = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.That(copied, Does.EndWith("<NxButton FullWidth=\"true\">Create an account</NxButton>"));
    }

    // ── Phone ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Phone_CopyAndShowCode_Are44px_AndDoNotWidenThePage()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("/combobox/examples");

        var toggle = Section("Grouping").Locator(".doc-example-code-toggle");
        await toggle.ClickAsync();
        var copy = Section("Grouping").Locator(".doc-code-copy");

        var t = (await toggle.BoundingBoxAsync())!;
        var c = (await copy.BoundingBoxAsync())!;
        Assert.That(t.Height, Is.GreaterThanOrEqualTo(44), "Show code height");
        Assert.That(c.Height, Is.GreaterThanOrEqualTo(44), "copy height");
        Assert.That(c.Width, Is.GreaterThanOrEqualTo(44), "copy width");
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"),
            Is.LessThanOrEqualTo(0));
    }
}
