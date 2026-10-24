using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxMaskedTextBoxTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxMaskedTextBox> RenderBound(List<string?> raised, string mask, string? initial = null,
        Action<ComponentParameterCollectionBuilder<NxMaskedTextBox>>? configure = null)
    {
        IRenderedComponent<NxMaskedTextBox>? cut = null;
        cut = Render<NxMaskedTextBox>(p =>
        {
            p.Add(x => x.Id, "m");
            p.Add(x => x.Mask, mask);
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (string? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    /// <summary>What nx-input.js reports: the whole masked text, then the change.</summary>
    private static async Task TypeAndCommitAsync(IRenderedComponent<NxMaskedTextBox> cut, string masked)
    {
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = masked });
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = masked });
    }

    private static string? Text(IRenderedComponent<NxMaskedTextBox> cut) => cut.Find("input").GetAttribute("value");

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_FieldWithMaskedValue_JsGetsTheMask()
    {
        var cut = RenderBound([], "(000) 000-0000", "2075550142", p => p.Add(x => x.Label, "Phone"));

        Assert.That(cut.Find(".nx-field.nx-maskedtextbox label").TextContent, Is.EqualTo("Phone"));
        Assert.That(Text(cut), Is.EqualTo("(207) 555-0142"));
        Assert.That(cut.Find("input").GetAttribute("inputmode"), Is.EqualTo("numeric"));
        var call = module.Invocations.Single(i => i.Identifier == "editMask");
        Assert.That(call.Arguments[1], Is.EqualTo("(000) 000-0000"));
        Assert.That(call.Arguments[2], Is.EqualTo("_"));
    }

    [Test]
    public void Empty_ShowsTheMask_OrNothingWithMaskOnFocus()
    {
        Assert.That(Text(RenderBound([], "00-0000")), Is.EqualTo("__-____"));

        var onFocus = RenderBound([], "00-0000", configure: p => p.Add(x => x.MaskOnFocus, true).Add(x => x.Placeholder, "YY-MM00"));
        Assert.That(Text(onFocus), Is.EqualTo(""));
        Assert.That(onFocus.Find("input").GetAttribute("placeholder"), Is.EqualTo("YY-MM00"));
    }

    [Test]
    public void LettersInMask_NoNumberPad()
    {
        var cut = RenderBound([], "LL-000");
        Assert.That(cut.Find("input").HasAttribute("inputmode"), Is.False);
    }

    // ── Values ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Commit_WithoutLiterals_TypedOnly()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "(000) 000-0000");

        await TypeAndCommitAsync(cut, "(207) 555-0142");

        Assert.That(raised, Is.EqualTo(new[] { "2075550142" }));
    }

    [Test]
    public async Task Commit_WithLiterals_AsErpBidNumber()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "00-0000", configure: p => p.Add(x => x.IncludeLiterals, true).Add(x => x.Prompt, ' '));

        await TypeAndCommitAsync(cut, "26-0412");
        await TypeAndCommitAsync(cut, "26-04  ");

        Assert.That(raised, Is.EqualTo(new[] { "26-0412", "26-04" }));
    }

    [Test]
    public async Task Commit_NothingTyped_IsNull_EnterCommits()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "00-0000", "26-0412", p => p.Add(x => x.IncludeLiterals, true));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "__-____" });
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(raised, Is.EqualTo(new string?[] { null }));
    }

    [Test]
    public async Task Clear_ButtonAndEscape()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "00-0000", "26-0412", p => p.Add(x => x.Clearable, true).Add(x => x.IncludeLiterals, true));

        await cut.Find(".nx-field-clear").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new string?[] { null }));
        Assert.That(Text(cut), Is.EqualTo("__-____"));

        var other = new List<string?>();
        var esc = RenderBound(other, "00-0000", "26-0412", p => p.Add(x => x.Clearable, true));
        await esc.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(other, Is.EqualTo(new string?[] { null }));
    }

    [Test]
    public void IsComplete_AllSlotsFilled()
    {
        Assert.That(RenderBound([], "00-0000", "26-0412", p => p.Add(x => x.IncludeLiterals, true)).Instance.IsComplete, Is.True);
        Assert.That(RenderBound([], "00-0000", "26-04", p => p.Add(x => x.IncludeLiterals, true)).Instance.IsComplete, Is.False);
    }

    [Test]
    public async Task Blur_MaskOnFocus_EmptyGoesBackToPlaceholder()
    {
        var cut = RenderBound([], "00-0000", configure: p => p.Add(x => x.MaskOnFocus, true));
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "__-____" });
        await cut.Find("input").BlurAsync(new FocusEventArgs());
        Assert.That(Text(cut), Is.EqualTo(""));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private sealed class Bid
    {
        public string? Number { get; set; }
    }

    [Test]
    public async Task Validation_AriaInvalid_AndNotifies()
    {
        var bid = new Bid();
        var context = new EditContext(bid);
        var store = new ValidationMessageStore(context);
        var changed = new List<string>();
        context.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier.FieldName);
        var cut = Render<NxMaskedTextBox>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Mask, "00-0000")
            .Add(x => x.Value, bid.Number)
            .Add(x => x.ValueChanged, v => bid.Number = v)
            .Add(x => x.ValueExpression, () => bid.Number));

        store.Add(context.Field(nameof(Bid.Number)), "Required");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));

        await TypeAndCommitAsync(cut, "26-0412");
        Assert.That(changed, Is.EqualTo(new[] { "Number" }));
        Assert.That(bid.Number, Is.EqualTo("260412"));
    }

    // ── MaskPattern ───────────────────────────────────────────────────────────

    [TestCase("00-0000", "260412", false, "26-0412")]
    [TestCase("00-0000", "26-0412", true, "26-0412")]
    [TestCase("00-0000", "26", false, "26-____")]
    [TestCase("LL-000", "ab123", false, "ab-123")]
    [TestCase("LL-000", "1a2b345", false, "ab-345")]
    [TestCase(@"\00-00", "1234", false, "01-23")]
    [TestCase("(000) 000-0000", null, false, "(___) ___-____")]
    public void Pattern_Format(string mask, string? value, bool literals, string expected) =>
        Assert.That(new MaskPattern(mask).Format(value, literals, '_'), Is.EqualTo(expected));

    [TestCase("00-0000", "26-0412", false, "260412")]
    [TestCase("00-0000", "26-0412", true, "26-0412")]
    [TestCase("00-0000", "26-04__", true, "26-04")]
    [TestCase("00-0000", "2_-0412", false, "2_0412")]
    [TestCase("00-0000", "__-____", true, null)]
    public void Pattern_Extract(string mask, string text, bool literals, string? expected) =>
        Assert.That(new MaskPattern(mask).Extract(text, literals, '_'), Is.EqualTo(expected));

    [TestCase('0', '5', true)]
    [TestCase('0', 'a', false)]
    [TestCase('9', ' ', true)]
    [TestCase('#', '-', true)]
    [TestCase('L', 'é', true)]
    [TestCase('L', '1', false)]
    [TestCase('?', ' ', true)]
    [TestCase('A', '7', true)]
    [TestCase('a', ' ', true)]
    [TestCase('&', ' ', false)]
    [TestCase('C', ' ', true)]
    public void Pattern_Rules(char rule, char c, bool accepts) =>
        Assert.That(MaskPattern.Accepts(rule, c), Is.EqualTo(accepts));
}
