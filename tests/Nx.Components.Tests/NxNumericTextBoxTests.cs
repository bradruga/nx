using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxNumericTextBoxTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Markup(string html) => b => b.AddMarkupContent(0, html);

    /// <summary>A bound numeric box: ValueChanged writes back like @bind-Value. en-US unless overridden.</summary>
    private IRenderedComponent<NxNumericTextBox<T>> RenderBound<T>(List<T?> raised, T? initial = default,
        Action<ComponentParameterCollectionBuilder<NxNumericTextBox<T>>>? configure = null, CultureInfo? culture = null)
    {
        IRenderedComponent<NxNumericTextBox<T>>? cut = null;
        cut = Render<NxNumericTextBox<T>>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.Culture, culture ?? EnUs);
            p.Add(x => x.ValueChanged, (T? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task ChangeAsync<T>(IRenderedComponent<NxNumericTextBox<T>> cut, string text) =>
        cut.Find("input").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });

    private static async Task TypeAndCommitAsync<T>(IRenderedComponent<NxNumericTextBox<T>> cut, string text)
    {
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
        await ChangeAsync(cut, text);
    }

    private static Task KeyAsync<T>(IRenderedComponent<NxNumericTextBox<T>> cut, string key) =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });

    private static string? Text<T>(IRenderedComponent<NxNumericTextBox<T>> cut) => cut.Find("input").GetAttribute("value");

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_SpinButtonInputInsideSharedFrame()
    {
        var cut = Render<NxNumericTextBox<int>>(p => p.Add(x => x.Label, "Quantity"));

        var input = cut.Find(".nx-field.nx-numeric .nx-numeric-field input.nx-numeric-input");
        Assert.That(input.GetAttribute("role"), Is.EqualTo("spinbutton"));
        Assert.That(input.GetAttribute("type"), Is.EqualTo("text"));
        Assert.That(cut.Find("label.nx-numeric-label").GetAttribute("for"), Is.EqualTo(input.Id));
        Assert.That(input.Id, Does.StartWith("nx-numeric-"));
    }

    [Test]
    public void SpinButtons_RenderedOutOfTabOrderWithLabels()
    {
        var cut = Render<NxNumericTextBox<int>>();

        var up = cut.Find(".nx-numeric-increase");
        var down = cut.Find(".nx-numeric-decrease");
        Assert.That(up.GetAttribute("aria-label"), Is.EqualTo("Increase"));
        Assert.That(down.GetAttribute("aria-label"), Is.EqualTo("Decrease"));
        Assert.That(up.GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(up.GetAttribute("aria-controls"), Is.EqualTo(cut.Find("input").Id));
    }

    [Test]
    public void ShowSpinButtons_False_HidesThem()
    {
        var cut = Render<NxNumericTextBox<int>>(p => p.Add(x => x.ShowSpinButtons, false));

        Assert.That(cut.FindAll(".nx-numeric-spin"), Is.Empty);
    }

    [Test]
    public void PlaceholderPrefixSuffix_LikeTextBox()
    {
        var cut = Render<NxNumericTextBox<decimal?>>(p => p
            .Add(x => x.Label, "Price")
            .Add(x => x.Placeholder, "0.00")
            .Add(x => x.Prefix, Markup("$"))
            .Add(x => x.Suffix, Markup("USD")));

        Assert.That(cut.Find("input").GetAttribute("placeholder"), Is.EqualTo("0.00"));
        Assert.That(cut.Find(".nx-numeric-prefix").TextContent, Is.EqualTo("$"));
        Assert.That(cut.Find(".nx-numeric-suffix").TextContent, Is.EqualTo("USD"));
    }

    [Test]
    public void UnsupportedType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxNumericTextBox<byte>>());
    }

    // ── Format and culture ────────────────────────────────────────────────────

    [Test]
    public void Format_AppliesWhileNotFocused()
    {
        var cut = RenderBound<decimal>([], 1234.5m, p => p.Add(x => x.Format, "N2"));

        Assert.That(Text(cut), Is.EqualTo("1,234.50"));
    }

    [Test]
    public void Culture_ChangesSeparators()
    {
        var cut = RenderBound<decimal>([], 1234.5m, p => p.Add(x => x.Format, "N2"), CultureInfo.GetCultureInfo("de-DE"));

        Assert.That(Text(cut), Is.EqualTo("1.234,50"));
    }

    [Test]
    public void Decimals_DefaultFormatShowsFixedPlaces()
    {
        var cut = RenderBound<decimal>([], 5m, p => p.Add(x => x.Decimals, 2));

        Assert.That(Text(cut), Is.EqualTo("5.00"));
    }

    [Test]
    public async Task Focus_EditTextIsSwappedInByTheBrowser_NotFromHere()
    {
        var cut = RenderBound<decimal>([], 1234.5m, p => p.Add(x => x.Format, "N2"));
        Assert.That(cut.Find("input").GetAttribute("data-nx-edit"), Is.EqualTo("1234.5"));

        // A swap rendered from here would land a round trip late, on whatever was typed by then.
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        Assert.That(Text(cut), Is.EqualTo("1,234.50"));

        // nx-input.js swaps the text and raises input, which @bind carries here.
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "1234.5" });
        Assert.That(Text(cut), Is.EqualTo("1234.5"));
    }

    [Test]
    public async Task Focus_ReadOnly_KeepsFormattedText()
    {
        var cut = RenderBound<decimal>([], 1250m, p => p.Add(x => x.Format, "N0").Add(x => x.ReadOnly, true));
        Assert.That(cut.Find("input").HasAttribute("data-nx-edit"), Is.False);

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Assert.That(Text(cut), Is.EqualTo("1,250"));
    }

    [Test]
    public async Task Blur_RestoresFormattedText()
    {
        var cut = RenderBound<decimal>([], 1234.5m, p => p.Add(x => x.Format, "N2"));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "1234.5" });
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        Assert.That(Text(cut), Is.EqualTo("1,234.50"));
    }

    // ── Typing and committing ─────────────────────────────────────────────────

    [Test]
    public async Task Change_CommitsParsedValue()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 0);

        await TypeAndCommitAsync(cut, "42");

        Assert.That(raised, Is.EqualTo(new[] { 42 }));
    }

    [Test]
    public async Task Typing_DoesNotCommitUntilChange()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 0);

        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "7" });

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("7"));
    }

    [Test]
    public async Task Enter_Commits()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 0);

        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "9" });
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new[] { 9 }));
    }

    [Test]
    public async Task InvalidText_Reverts()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 5);

        await TypeAndCommitAsync(cut, "abc");

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("5"));
    }

    [Test]
    public async Task EmptyText_Nullable_CommitsNull()
    {
        var raised = new List<int?>();
        var cut = RenderBound<int?>(raised, 5);

        await TypeAndCommitAsync(cut, "");

        Assert.That(raised, Is.EqualTo(new int?[] { null }));
    }

    [Test]
    public async Task EmptyText_NonNullable_Reverts()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 5);

        await TypeAndCommitAsync(cut, "");

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("5"));
    }

    [TestCase("$1,234.50", 1234.5)]
    [TestCase("15%", 15)]
    [TestCase("(12)", -12)]
    [TestCase(" 7.25 ", 7.25)]
    public async Task Parse_AcceptsCurrencyPercentAndParentheses(string text, double expected)
    {
        var raised = new List<decimal>();
        var cut = RenderBound(raised, 0m);

        await TypeAndCommitAsync(cut, text);

        Assert.That(raised, Is.EqualTo(new[] { (decimal)expected }));
    }

    [TestCase("de-DE", "1.234,50\u00A0€", 1234.5)]
    [TestCase("fr-CA", "1\u202F234,50\u00A0$", 1234.5)]
    [TestCase("de-DE", "-7,25", -7.25)]
    public async Task Parse_FormattedTextInOtherCultures(string culture, string text, double expected)
    {
        var raised = new List<decimal>();
        var cut = RenderBound(raised, 0m, culture: CultureInfo.GetCultureInfo(culture));

        await TypeAndCommitAsync(cut, text);

        Assert.That(raised, Is.EqualTo(new[] { (decimal)expected }));
    }

    [Test]
    public void EditText_FormattedCurrency_InCultureWithNoBreakSpaces()
    {
        var cut = RenderBound<decimal>([], 1234.5m, p => p.Add(x => x.Format, "C"), CultureInfo.GetCultureInfo("de-DE"));

        Assert.That(cut.Find("input").GetAttribute("data-nx-edit"), Is.EqualTo("1234,5"));
    }

    [Test]
    public async Task Commit_ClampsToMinAndMax()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 5, p => p.Add(x => x.Min, 0).Add(x => x.Max, 10));

        await TypeAndCommitAsync(cut, "25");
        await TypeAndCommitAsync(cut, "-3");

        Assert.That(raised, Is.EqualTo(new[] { 10, 0 }));
    }

    [Test]
    public async Task Commit_RoundsToDecimals()
    {
        var raised = new List<decimal>();
        var cut = RenderBound(raised, 0m, p => p.Add(x => x.Decimals, 2));

        await TypeAndCommitAsync(cut, "3.14159");

        Assert.That(raised, Is.EqualTo(new[] { 3.14m }));
    }

    [Test]
    public async Task Commit_IntegerRoundsAwayFromZero()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 0);

        await TypeAndCommitAsync(cut, "2.5");

        Assert.That(raised, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public async Task Double_RoundTrips()
    {
        var raised = new List<double>();
        var cut = RenderBound(raised, 0.0);

        await TypeAndCommitAsync(cut, "0.125");

        Assert.That(raised, Is.EqualTo(new[] { 0.125 }));
    }

    [Test]
    public async Task Long_BeyondIntRange_RoundTrips()
    {
        var raised = new List<long>();
        var cut = RenderBound(raised, 0L);

        await TypeAndCommitAsync(cut, "5000000000");

        Assert.That(raised, Is.EqualTo(new[] { 5_000_000_000L }));
    }

    [Test]
    public async Task Short_ClampsToTheTypesRange()
    {
        var raised = new List<short>();
        var cut = RenderBound(raised, (short)0);

        await TypeAndCommitAsync(cut, "99999");

        Assert.That(raised, Is.EqualTo(new[] { short.MaxValue }));
    }

    [Test]
    public async Task Float_RoundTrips()
    {
        var raised = new List<float>();
        var cut = RenderBound(raised, 0f);

        await TypeAndCommitAsync(cut, "2.5");

        Assert.That(raised, Is.EqualTo(new[] { 2.5f }));
    }

    [Test]
    public async Task Focused_ParentRerenderWithSameValue_KeepsTypedText()
    {
        var cut = RenderBound<int>([], 5);

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "7" });
        cut.Render(p => p.Add(x => x.Value, 5));

        Assert.That(Text(cut), Is.EqualTo("7"));
    }

    [Test]
    public async Task Spin_DecreaseClickStepsDownByStep()
    {
        var raised = new List<decimal>();
        var cut = RenderBound(raised, 1m, p => p.Add(x => x.Step, 0.25m));

        var press = cut.InvokeAsync(() => cut.Find(".nx-numeric-decrease").TriggerEventAsync("onpointerdown", new PointerEventArgs()));
        await cut.Find(".nx-numeric-decrease").TriggerEventAsync("onpointerup", new PointerEventArgs());
        await press;

        Assert.That(raised, Is.EqualTo(new[] { 0.75m }));
    }

    [Test]
    public void ExternalValueChange_UpdatesText()
    {
        var cut = RenderBound<int>([], 1);

        cut.Render(p => p.Add(x => x.Value, 99));

        Assert.That(Text(cut), Is.EqualTo("99"));
    }

    // ── Min / Max presence ────────────────────────────────────────────────────

    [Test]
    public void MinZero_OnNonNullableInt_IsHonored()
    {
        var cut = Render<NxNumericTextBox<int>>(p => p.Add(x => x.Min, 0));

        Assert.That(cut.Find("input").GetAttribute("aria-valuemin"), Is.EqualTo("0"));
    }

    [Test]
    public async Task NoMin_AllowsNegatives()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 0);

        await TypeAndCommitAsync(cut, "-5");

        Assert.That(raised, Is.EqualTo(new[] { -5 }));
        Assert.That(cut.Find("input").HasAttribute("aria-valuemin"), Is.False);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Keyboard_ArrowsStepByStep_PageKeysByTen()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 10, p => p.Add(x => x.Step, 2));

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "PageUp");
        await KeyAsync(cut, "PageDown");

        Assert.That(raised, Is.EqualTo(new[] { 12, 10, 8, 28, 8 }));
    }

    [Test]
    public async Task Keyboard_StepsFromTypedText()
    {
        var raised = new List<decimal>();
        var cut = RenderBound(raised, 1m, p => p.Add(x => x.Step, 0.25m));

        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "4" });
        await KeyAsync(cut, "ArrowUp");

        Assert.That(raised, Is.EqualTo(new[] { 4.25m }));
    }

    [Test]
    public async Task Keyboard_StopsAtMax()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 9, p => p.Add(x => x.Max, 10));

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "ArrowUp");

        Assert.That(raised, Is.EqualTo(new[] { 10 }));
    }

    [Test]
    public async Task Keyboard_NullableFromEmpty_StartsAtZero()
    {
        var raised = new List<int?>();
        var cut = RenderBound<int?>(raised, null);

        await KeyAsync(cut, "ArrowUp");

        Assert.That(raised, Is.EqualTo(new int?[] { 1 }));
    }

    [Test]
    public async Task Keyboard_ReadOnly_DoesNothing()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 1, p => p.Add(x => x.ReadOnly, true));

        await KeyAsync(cut, "ArrowUp");

        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.True);
    }

    [Test]
    public void Keyboard_ArrowAndPageDefaultsCancelledInJs()
    {
        Render<NxNumericTextBox<int>>();

        var call = module.Invocations.Single(i => i.Identifier == "preventKeys");
        Assert.That((string[])call.Arguments[1]!, Is.EquivalentTo(new[] { "ArrowUp", "ArrowDown", "PageUp", "PageDown" }));
    }

    [Test]
    public void Keyboard_JsMarksTheClearingEscapeAsUsed()
    {
        Render<NxNumericTextBox<int?>>(p => p.Add(x => x.Clearable, true).Add(x => x.Value, 5));

        Assert.That(module.Invocations.Count(i => i.Identifier == "escapeClears"), Is.EqualTo(1));
    }

    // ── Spin buttons ──────────────────────────────────────────────────────────

    [Test]
    public async Task Spin_ClickStepsOnce()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 1);

        var press = cut.InvokeAsync(() => cut.Find(".nx-numeric-increase").TriggerEventAsync("onpointerdown", new PointerEventArgs()));
        await cut.Find(".nx-numeric-increase").TriggerEventAsync("onpointerup", new PointerEventArgs());
        await press;

        Assert.That(raised, Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public async Task Spin_HoldRepeatsUntilReleased()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 0);

        _ = cut.InvokeAsync(() => cut.Find(".nx-numeric-increase").TriggerEventAsync("onpointerdown", new PointerEventArgs()));
        cut.WaitForState(() => raised.Count >= 4, TimeSpan.FromSeconds(3));
        await cut.InvokeAsync(() => cut.Find(".nx-numeric-increase").TriggerEventAsync("onpointerup", new PointerEventArgs()));
        var released = raised.Count;
        await Task.Delay(300);

        Assert.That(raised.Count, Is.EqualTo(released), "spinning continued after release");
        Assert.That(raised, Is.EqualTo(Enumerable.Range(1, released)));
    }

    [Test]
    public async Task Spin_DisabledAtLimits()
    {
        var cut = RenderBound<int>([], 10, p => p.Add(x => x.Min, 0).Add(x => x.Max, 10));

        Assert.That(cut.Find(".nx-numeric-increase").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-numeric-decrease").HasAttribute("disabled"), Is.False);

        await KeyAsync(cut, "PageDown");
        Assert.That(cut.Find(".nx-numeric-decrease").HasAttribute("disabled"), Is.True);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Clear_Nullable_SetsNull()
    {
        var raised = new List<decimal?>();
        var cut = RenderBound<decimal?>(raised, 12.5m, p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-numeric-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new decimal?[] { null }));
        Assert.That(Text(cut), Is.EqualTo(""));
        Assert.That(cut.FindAll(".nx-numeric-clear"), Is.Empty);
    }

    [Test]
    public async Task Clear_NonNullable_SetsMinOrZero()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 8, p => p.Add(x => x.Clearable, true).Add(x => x.Min, 1));

        await KeyAsync(cut, "Escape");

        Assert.That(raised, Is.EqualTo(new[] { 1 }));
    }

    // ── State and attributes ──────────────────────────────────────────────────

    [Test]
    public void Disabled_DisablesInputAndSpinButtons()
    {
        var cut = Render<NxNumericTextBox<int>>(p => p.Add(x => x.Disabled, true));

        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll(".nx-numeric-spin-button[disabled]"), Has.Count.EqualTo(2));
        Assert.That(cut.Find(".nx-numeric").ClassList, Does.Contain("nx-numeric-disabled"));
    }

    [TestCase(true, 0, "numeric")]
    [TestCase(false, 0, "decimal")]
    public void InputMode_FollowsTypeAndMin(bool integral, int min, string expected)
    {
        var mode = integral
            ? Render<NxNumericTextBox<int>>(p => p.Add(x => x.Min, min)).Find("input").GetAttribute("inputmode")
            : Render<NxNumericTextBox<decimal>>(p => p.Add(x => x.Min, min)).Find("input").GetAttribute("inputmode");

        Assert.That(mode, Is.EqualTo(expected));
    }

    [Test]
    public void InputMode_NegativesAllowed_UsesFullKeyboard()
    {
        var cut = Render<NxNumericTextBox<int>>();

        Assert.That(cut.Find("input").GetAttribute("inputmode"), Is.EqualTo("text"));
    }

    [Test]
    public void Aria_ValueNowAndText()
    {
        var cut = RenderBound<decimal>([], 1234.5m, p => p.Add(x => x.Format, "C").Add(x => x.Max, 5000m));

        var input = cut.Find("input");
        Assert.That(input.GetAttribute("aria-valuenow"), Is.EqualTo("1234.5"));
        Assert.That(input.GetAttribute("aria-valuetext"), Is.EqualTo("$1,234.50"));
        Assert.That(input.GetAttribute("aria-valuemax"), Is.EqualTo("5000"));
    }

    [Test]
    public void AdditionalAttributes_GoToInput_ClassStyleToFrame()
    {
        var cut = Render<NxNumericTextBox<int>>(p => p
            .Add(x => x.Class, "narrow")
            .Add(x => x.Style, "width: 120px")
            .AddUnmatched("name", "qty"));

        Assert.That(cut.Find(".nx-numeric").ClassList, Does.Contain("narrow"));
        Assert.That(cut.Find(".nx-numeric").GetAttribute("style"), Is.EqualTo("width: 120px"));
        Assert.That(cut.Find("input").GetAttribute("name"), Is.EqualTo("qty"));
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task SelectOnFocus_IsDoneInTheBrowserOnFocus()
    {
        var cut = RenderBound<int>([], 5, p => p.Add(x => x.SelectOnFocus, true));

        Assert.That(module.Invocations.Any(i => i.Identifier == "editOnFocus" && (bool)i.Arguments[1]!), Is.True);
        await cut.Find("input").FocusAsync(new FocusEventArgs());

        // Selecting from here, a round trip after focus, would select what had been typed by then.
        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Not.Contain("select"));
    }

    [Test]
    public async Task SelectOnFocus_Off_DoesNotSelect()
    {
        var cut = RenderBound<int>([], 5);
        Assert.That(module.Invocations.Any(i => i.Identifier == "editOnFocus" && !(bool)i.Arguments[1]!), Is.True);

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Not.Contain("select"));
    }

    [Test]
    public void AutoFocus_Focuses()
    {
        Render<NxNumericTextBox<int>>(p => p.Add(x => x.AutoFocus, true));

        JSInterop.VerifyFocusAsyncInvoke();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public int? Quantity { get; set; }
    }

    [Test]
    public async Task Validation_ChangeMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxNumericTextBox<int?>>(c => c
                .Add(x => x.Value, model.Quantity)
                .Add(x => x.ValueChanged, (int? v) => model.Quantity = v)
                .Add(x => x.ValueExpression, () => model.Quantity)));
        var cut = host.FindComponent<NxNumericTextBox<int?>>();

        await TypeAndCommitAsync(cut, "3");
        Assert.That(context.IsModified(context.Field(nameof(Model.Quantity))), Is.True);
        Assert.That(cut.Find("input").ClassList, Does.Contain("modified"));

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Quantity)), "Too few.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-numeric").ClassList, Does.Contain("nx-numeric-invalid"));
    }
}
