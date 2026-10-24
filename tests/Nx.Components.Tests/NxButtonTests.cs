using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxButtonTests : BunitContext
{
    private static RenderFragment Markup(string html) => b => b.AddMarkupContent(0, html);

    private IRenderedComponent<NxButton> RenderButton(Action<ComponentParameterCollectionBuilder<NxButton>>? configure = null) =>
        Render<NxButton>(p =>
        {
            p.AddChildContent("Save");
            configure?.Invoke(p);
        });

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_Defaults_NativeSecondaryMediumTypeButton()
    {
        var button = RenderButton().Find("button");

        Assert.That(button.GetAttribute("type"), Is.EqualTo("button"));
        Assert.That(button.ClassList, Does.Contain("nx-button"));
        Assert.That(button.ClassList, Does.Contain("nx-button-secondary"));
        Assert.That(button.ClassList, Does.Contain("nx-button-md"));
        Assert.That(button.HasAttribute("disabled"), Is.False);
    }

    [Test]
    public void Render_ChildContent_InLabel()
    {
        var cut = RenderButton();

        Assert.That(cut.Find(".nx-button-label").TextContent, Is.EqualTo("Save"));
    }

    [Test]
    public void Render_NoChildContentNoIcon_RendersEmptyButtonWithoutLabel()
    {
        var cut = Render<NxButton>();

        Assert.That(cut.FindAll(".nx-button-label"), Is.Empty);
        Assert.That(cut.Find("button").ClassList, Does.Not.Contain("nx-button-icon-only"));
    }

    // ── Parameters ────────────────────────────────────────────────────────────

    [TestCase(NxButtonVariant.Primary,   "nx-button-primary")]
    [TestCase(NxButtonVariant.Secondary, "nx-button-secondary")]
    [TestCase(NxButtonVariant.Danger,    "nx-button-danger")]
    [TestCase(NxButtonVariant.Ghost,     "nx-button-ghost")]
    [TestCase(NxButtonVariant.Link,      "nx-button-link")]
    [TestCase(NxButtonVariant.Success,   "nx-button-success")]
    [TestCase(NxButtonVariant.Warning,   "nx-button-warning")]
    [TestCase(NxButtonVariant.Info,      "nx-button-info")]
    public void Variant_AppliesClass(NxButtonVariant variant, string expected)
    {
        var button = RenderButton(p => p.Add(x => x.Variant, variant)).Find("button");

        Assert.That(button.ClassList, Does.Contain(expected));
    }

    [TestCase(NxButtonSize.Small,  "nx-button-sm")]
    [TestCase(NxButtonSize.Medium, "nx-button-md")]
    [TestCase(NxButtonSize.Large,  "nx-button-lg")]
    public void Size_AppliesClass(NxButtonSize size, string expected)
    {
        var button = RenderButton(p => p.Add(x => x.Size, size)).Find("button");

        Assert.That(button.ClassList, Does.Contain(expected));
    }

    [TestCase(NxButtonType.Button, "button")]
    [TestCase(NxButtonType.Submit, "submit")]
    [TestCase(NxButtonType.Reset,  "reset")]
    public void Type_SetsTypeAttribute(NxButtonType type, string expected)
    {
        var button = RenderButton(p => p.Add(x => x.Type, type)).Find("button");

        Assert.That(button.GetAttribute("type"), Is.EqualTo(expected));
    }

    [Test]
    public void FullWidth_AppliesClass()
    {
        var button = RenderButton(p => p.Add(x => x.FullWidth, true)).Find("button");

        Assert.That(button.ClassList, Does.Contain("nx-button-full-width"));
    }

    [Test]
    public void ClassAndStyle_AreAppliedAlongsideComponentClasses()
    {
        var button = RenderButton(p => p
            .Add(x => x.Class, "my-class")
            .Add(x => x.Style, "margin: 4px")).Find("button");

        Assert.That(button.ClassList, Does.Contain("my-class"));
        Assert.That(button.ClassList, Does.Contain("nx-button"));
        Assert.That(button.GetAttribute("style"), Is.EqualTo("margin: 4px"));
    }

    [Test]
    public void AdditionalAttributes_PassThroughToButton()
    {
        var button = RenderButton(p => p
            .AddUnmatched("id", "save-button")
            .AddUnmatched("data-row", "3")
            .AddUnmatched("form", "order-form")).Find("button");

        Assert.That(button.Id, Is.EqualTo("save-button"));
        Assert.That(button.GetAttribute("data-row"), Is.EqualTo("3"));
        Assert.That(button.GetAttribute("form"), Is.EqualTo("order-form"));
    }

    [Test]
    public void Title_SetsTitleAttribute()
    {
        var button = RenderButton(p => p.Add(x => x.Title, "Save the order")).Find("button");

        Assert.That(button.GetAttribute("title"), Is.EqualTo("Save the order"));
    }

    // ── Icon ──────────────────────────────────────────────────────────────────

    [Test]
    public void Icon_Start_RendersBeforeLabel()
    {
        var cut = RenderButton(p => p.Add(x => x.Icon, Markup("<svg class='i'></svg>")));

        var children = cut.Find("button").Children;
        Assert.That(children[0].ClassList, Does.Contain("nx-button-icon"));
        Assert.That(children[1].ClassList, Does.Contain("nx-button-label"));
        Assert.That(children[0].QuerySelector("svg.i"), Is.Not.Null);
    }

    [Test]
    public void Icon_End_RendersAfterLabel()
    {
        var cut = RenderButton(p => p
            .Add(x => x.Icon, Markup("<svg></svg>"))
            .Add(x => x.IconPosition, NxIconPosition.End));

        var children = cut.Find("button").Children;
        Assert.That(children[0].ClassList, Does.Contain("nx-button-label"));
        Assert.That(children[1].ClassList, Does.Contain("nx-button-icon"));
    }

    [Test]
    public void Icon_WithoutChildContent_IsIconOnly()
    {
        var cut = Render<NxButton>(p => p
            .Add(x => x.Icon, Markup("<svg></svg>"))
            .Add(x => x.AriaLabel, "Delete"));

        var button = cut.Find("button");
        Assert.That(button.ClassList, Does.Contain("nx-button-icon-only"));
        Assert.That(cut.FindAll(".nx-button-label"), Is.Empty);
        Assert.That(button.GetAttribute("aria-label"), Is.EqualTo("Delete"));
    }

    [Test]
    public void Icon_WithChildContent_IsNotIconOnly()
    {
        var button = RenderButton(p => p.Add(x => x.Icon, Markup("<svg></svg>"))).Find("button");

        Assert.That(button.ClassList, Does.Not.Contain("nx-button-icon-only"));
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    [Test]
    public void Loading_ReplacesStartIconWithSpinner()
    {
        var cut = RenderButton(p => p
            .Add(x => x.Icon, Markup("<svg></svg>"))
            .Add(x => x.Loading, true));

        Assert.That(cut.FindAll(".nx-button-icon"), Is.Empty);
        Assert.That(cut.Find("button").Children[0].ClassList, Does.Contain("nx-spinner"));
        Assert.That(cut.Find("button").ClassList, Does.Contain("nx-button-loading"));
    }

    [Test]
    public void Loading_WithEndIcon_SpinnerAtEnd()
    {
        var cut = RenderButton(p => p
            .Add(x => x.Icon, Markup("<svg></svg>"))
            .Add(x => x.IconPosition, NxIconPosition.End)
            .Add(x => x.Loading, true));

        var children = cut.Find("button").Children;
        Assert.That(children[0].ClassList, Does.Contain("nx-button-label"));
        Assert.That(children[1].ClassList, Does.Contain("nx-spinner"));
    }

    [Test]
    public void Loading_IconOnly_SpinnerReplacesIcon()
    {
        var cut = Render<NxButton>(p => p
            .Add(x => x.Icon, Markup("<svg></svg>"))
            .Add(x => x.AriaLabel, "Refresh")
            .Add(x => x.Loading, true));

        Assert.That(cut.FindAll(".nx-spinner"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll(".nx-button-icon"), Is.Empty);
    }

    [Test]
    public void Loading_SetsAriaBusyAndAriaDisabled_ButStaysFocusable()
    {
        var button = RenderButton(p => p.Add(x => x.Loading, true)).Find("button");

        Assert.That(button.GetAttribute("aria-busy"), Is.EqualTo("true"));
        Assert.That(button.GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(button.HasAttribute("disabled"), Is.False);
    }

    [Test]
    public void Loading_WithLoadingText_ReplacesLabel()
    {
        var cut = RenderButton(p => p
            .Add(x => x.Loading, true)
            .Add(x => x.LoadingText, "Saving…"));

        Assert.That(cut.Find(".nx-button-label").TextContent, Is.EqualTo("Saving…"));
    }

    [Test]
    public void LoadingText_IgnoredWhenNotLoading()
    {
        var cut = RenderButton(p => p.Add(x => x.LoadingText, "Saving…"));

        Assert.That(cut.Find(".nx-button-label").TextContent, Is.EqualTo("Save"));
    }

    [Test]
    public async Task Loading_SuppressesOnClick()
    {
        var clicks = 0;
        var cut = RenderButton(p => p
            .Add(x => x.Loading, true)
            .Add(x => x.OnClick, () => clicks++));

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.That(clicks, Is.Zero);
    }

    // ── Disabled ──────────────────────────────────────────────────────────────

    [Test]
    public void Disabled_RendersNativeDisabled_WithoutAriaDisabled()
    {
        var button = RenderButton(p => p.Add(x => x.Disabled, true)).Find("button");

        Assert.That(button.HasAttribute("disabled"), Is.True);
        Assert.That(button.HasAttribute("aria-disabled"), Is.False);
    }

    [Test]
    public async Task Disabled_SuppressesOnClick()
    {
        var clicks = 0;
        var cut = RenderButton(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.OnClick, () => clicks++));

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.That(clicks, Is.Zero);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Click_RaisesOnClickWithMouseArgs()
    {
        MouseEventArgs? received = null;
        var cut = RenderButton(p => p.Add(x => x.OnClick, (MouseEventArgs e) => received = e));

        await cut.Find("button").ClickAsync(new MouseEventArgs { Detail = 1, ClientX = 12 });

        Assert.That(received, Is.Not.Null);
        Assert.That(received!.ClientX, Is.EqualTo(12));
    }

    [Test]
    public async Task BusyGuard_IgnoresClicksWhileHandlerRuns()
    {
        var runs = 0;
        var gate = new TaskCompletionSource();
        var cut = RenderButton(p => p.Add(x => x.OnClick, async () => { runs++; await gate.Task; }));

        var first = cut.Find("button").ClickAsync(new MouseEventArgs());
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.That(runs, Is.EqualTo(1));
        Assert.That(cut.Instance.IsBusy, Is.True);
        Assert.That(cut.Find("button").GetAttribute("aria-disabled"), Is.EqualTo("true"));

        gate.SetResult();
        await first;

        Assert.That(cut.Instance.IsBusy, Is.False);
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.That(runs, Is.EqualTo(2));
    }

    [Test]
    public async Task BusyGuard_Off_RunsHandlerForEveryClick()
    {
        var runs = 0;
        var gate = new TaskCompletionSource();
        var cut = RenderButton(p => p
            .Add(x => x.PreventDoubleClick, false)
            .Add(x => x.OnClick, async () => { runs++; await gate.Task; }));

        var first = cut.Find("button").ClickAsync(new MouseEventArgs());
        var second = cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.That(runs, Is.EqualTo(2));
        gate.SetResult();
        await Task.WhenAll(first, second);
    }

    [Test]
    public void BusyGuard_HandlerThrows_ReleasesGuard()
    {
        var runs = 0;
        var cut = RenderButton(p => p.Add(x => x.OnClick, () =>
        {
            runs++;
            throw new InvalidOperationException("boom");
        }));

        Assert.ThrowsAsync<InvalidOperationException>(() => cut.Find("button").ClickAsync(new MouseEventArgs()));
        Assert.That(cut.Instance.IsBusy, Is.False);

        Assert.ThrowsAsync<InvalidOperationException>(() => cut.Find("button").ClickAsync(new MouseEventArgs()));
        Assert.That(runs, Is.EqualTo(2));
    }

    [Test]
    public async Task AutoLoading_ShowsSpinnerOnlyWhileHandlerRuns()
    {
        var gate = new TaskCompletionSource();
        var cut = RenderButton(p => p
            .Add(x => x.AutoLoading, true)
            .Add(x => x.OnClick, () => gate.Task));

        Assert.That(cut.FindAll(".nx-spinner"), Is.Empty);

        var click = cut.Find("button").ClickAsync(new MouseEventArgs());
        cut.WaitForState(() => cut.FindAll(".nx-spinner").Count == 1);
        Assert.That(cut.Find("button").GetAttribute("aria-busy"), Is.EqualTo("true"));

        gate.SetResult();
        await click;

        cut.WaitForState(() => cut.FindAll(".nx-spinner").Count == 0);
        Assert.That(cut.Find("button").HasAttribute("aria-busy"), Is.False);
    }

    [Test]
    public async Task WithoutAutoLoading_BusyHandlerShowsNoSpinner()
    {
        var gate = new TaskCompletionSource();
        var cut = RenderButton(p => p.Add(x => x.OnClick, () => gate.Task));

        var click = cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-spinner"), Is.Empty);
        gate.SetResult();
        await click;
    }

    // ── Keyboard and accessibility ────────────────────────────────────────────

    [Test]
    public void Keyboard_IsNativeButtonInTabOrder()
    {
        // Enter and Space activation come from the browser for a native <button>; tested end to end in Nx.Playwright.
        var button = RenderButton().Find("button");

        Assert.That(button.TagName, Is.EqualTo("BUTTON"));
        Assert.That(button.HasAttribute("tabindex"), Is.False);
    }

    [Test]
    public void Accessibility_IconAndSpinnerAreHiddenFromScreenReaders()
    {
        var cut = RenderButton(p => p.Add(x => x.Icon, Markup("<svg></svg>")));
        Assert.That(cut.Find(".nx-button-icon").GetAttribute("aria-hidden"), Is.EqualTo("true"));

        cut.Render(p => p.Add(x => x.Loading, true));
        Assert.That(cut.Find(".nx-spinner").GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public void Accessibility_NotBusy_NoAriaBusyOrAriaDisabled()
    {
        var button = RenderButton().Find("button");

        Assert.That(button.HasAttribute("aria-busy"), Is.False);
        Assert.That(button.HasAttribute("aria-disabled"), Is.False);
    }

    [Test]
    public async Task FocusAsync_FocusesButtonElement()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderButton();

        await cut.InvokeAsync(() => cut.Instance.FocusAsync().AsTask());

        JSInterop.VerifyFocusAsyncInvoke();
    }
}
