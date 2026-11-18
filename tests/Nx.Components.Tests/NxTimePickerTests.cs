using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTimePickerTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-picker.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxTimePicker<T>> RenderBound<T>(List<T?> raised, T? initial = default,
        Action<ComponentParameterCollectionBuilder<NxTimePicker<T>>>? configure = null, CultureInfo? culture = null)
    {
        IRenderedComponent<NxTimePicker<T>>? cut = null;
        cut = Render<NxTimePicker<T>>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.Culture, culture ?? EnUs);
            p.Add(x => x.Id, "tp");
            p.Add(x => x.ValueChanged, (T? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static async Task TypeAndCommitAsync<T>(IRenderedComponent<NxTimePicker<T>> cut, string text)
    {
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
        await cut.Find("input").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });
    }

    private static Task KeyAsync<T>(IRenderedComponent<NxTimePicker<T>> cut, string key) =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });

    private static string? Text<T>(IRenderedComponent<NxTimePicker<T>> cut) => cut.Find("input").GetAttribute("value");

    // ── Rendering and format ──────────────────────────────────────────────────

    [Test]
    public void Render_ComboboxWithListPopup()
    {
        var cut = RenderBound<TimeOnly?>([], new TimeOnly(14, 30), p => p.Add(x => x.Label, "Arrives"));

        var input = cut.Find(".nx-field.nx-timepicker input.nx-timepicker-input");
        Assert.That(input.GetAttribute("role"), Is.EqualTo("combobox"));
        Assert.That(input.GetAttribute("aria-haspopup"), Is.EqualTo("listbox"));
        Assert.That(Text(cut), Is.EqualTo("2:30 PM"));
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("hh:mm AM/PM"));
    }

    [Test]
    public void Clock_FromCultureOrUse24Hour_MaskOnlyFor24()
    {
        var de = RenderBound<TimeOnly?>([], new TimeOnly(14, 30), culture: DeDe);
        Assert.That(Text(de), Is.EqualTo("14:30"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "mask" && (string?)i.Arguments[1] == "99:99"), Is.True);

        var forced = RenderBound<TimeOnly?>([], new TimeOnly(9, 5), p => p.Add(x => x.Use24Hour, true));
        Assert.That(Text(forced), Is.EqualTo("09:05"));
        Assert.That(forced.Find("input").GetAttribute("inputmode"), Is.EqualTo("numeric"));
    }

    [Test]
    public void Unsupported_Type_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxTimePicker<int>>());
    }

    // ── Typing ────────────────────────────────────────────────────────────────

    [TestCase("9:30 PM", 21, 30)]
    [TestCase("930p", 21, 30)]
    [TestCase("930", 9, 30)]
    [TestCase("1430", 14, 30)]
    [TestCase("9", 9, 0)]
    [TestCase("12a", 0, 0)]
    [TestCase("12:15 pm", 12, 15)]
    [TestCase("21:30", 21, 30)]
    public async Task Typed_LooseForms(string text, int hour, int minute)
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised);

        await TypeAndCommitAsync(cut, text);

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(hour, minute) }));
    }

    [TestCase("25:00")]
    [TestCase("noon-ish")]
    public async Task Typed_Invalid_Reverts(string text)
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, new TimeOnly(8, 0));

        await TypeAndCommitAsync(cut, text);

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("8:00 AM"));
    }

    [Test]
    public async Task Typed_OutsideMinMax_MovesToLimit()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, null, p => p.Add(x => x.Min, new TimeOnly(6, 0)).Add(x => x.Max, new TimeOnly(18, 0)));

        await TypeAndCommitAsync(cut, "5:00 AM");
        await TypeAndCommitAsync(cut, "11 pm");

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(6, 0), new TimeOnly(18, 0) }));
    }

    [Test]
    public async Task TimeSpanAndDateTime_Convert()
    {
        var spans = new List<TimeSpan>();
        var span = RenderBound(spans, new TimeSpan(9, 0, 0));
        await TypeAndCommitAsync(span, "10:15 AM");

        var stamps = new List<DateTime>();
        var stamp = RenderBound(stamps, new DateTime(2026, 10, 23, 16, 0, 0));
        await TypeAndCommitAsync(stamp, "7:45 AM");

        Assert.That(spans, Is.EqualTo(new[] { new TimeSpan(10, 15, 0) }));
        Assert.That(stamps, Is.EqualTo(new[] { new DateTime(2026, 10, 23, 7, 45, 0) }));
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task List_StepsFromMinToMax()
    {
        var cut = RenderBound<TimeOnly?>([], null, p => p
            .Add(x => x.Step, 15)
            .Add(x => x.Min, new TimeOnly(6, 0))
            .Add(x => x.Max, new TimeOnly(7, 0)));

        await cut.Find(".nx-timepicker-toggle").ClickAsync(new MouseEventArgs());

        var options = cut.FindAll(".nx-timepicker-popup [role=option]").Select(o => o.TextContent.Trim());
        Assert.That(options, Is.EqualTo(new[] { "6:00 AM", "6:15 AM", "6:30 AM", "6:45 AM", "7:00 AM" }));
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Open_HighlightsValueOrNextStep()
    {
        var cut = RenderBound<TimeOnly?>([], new TimeOnly(9, 10));

        await KeyAsync(cut, "ArrowDown");

        // 9:10 is between steps: the highlight is the next one, 9:30 (index 19).
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Is.EqualTo("tp-option-19"));
        Assert.That(cut.Find("#tp-option-19").ClassList, Does.Contain("nx-timepicker-item-active"));
    }

    [Test]
    public async Task Arrows_ThenEnter_PicksHighlighted()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, new TimeOnly(7, 0), p => p.Add(x => x.Step, 15).Add(x => x.Min, new TimeOnly(6, 0)));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(7, 30) }));
        Assert.That(cut.FindAll(".nx-timepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task TypedWhileOpen_EnterTakesTypedTime()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, new TimeOnly(7, 0));

        await KeyAsync(cut, "ArrowDown");
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "8:10 AM" });
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Is.EqualTo("tp-option-17"));   // 8:30
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(8, 10) }));
    }

    [Test]
    public async Task ClickOption_Picks_EscapeAndBlurClose()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised);

        await KeyAsync(cut, "ArrowDown");
        await cut.Find("#tp-option-2").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(1, 0) }));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Escape");
        Assert.That(cut.FindAll(".nx-timepicker-popup"), Is.Empty);
        await KeyAsync(cut, "ArrowDown");
        await cut.Find("input").BlurAsync(new FocusEventArgs());
        Assert.That(cut.FindAll(".nx-timepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task Clearable_Clears()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, new TimeOnly(7, 0), p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-timepicker-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { null }));
        Assert.That(Text(cut), Is.EqualTo(""));
    }

    [Test]
    public async Task Typed_Empty_NullForNullable_RevertsOtherwise()
    {
        var nullable = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(nullable, new TimeOnly(8, 0));
        await TypeAndCommitAsync(cut, "");

        var plain = new List<TimeOnly>();
        var plainCut = RenderBound(plain, new TimeOnly(8, 0));
        await TypeAndCommitAsync(plainCut, "");

        Assert.That(nullable, Is.EqualTo(new TimeOnly?[] { null }));
        Assert.That(plain, Is.Empty);
        Assert.That(Text(plainCut), Is.EqualTo("8:00 AM"));
    }

    [Test]
    public async Task Enter_CommitsTypedText()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised);

        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "1430" });
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(14, 30) }));
        Assert.That(Text(cut), Is.EqualTo("2:30 PM"));
    }

    [Test]
    public async Task Blur_CommitsTypedText_RaisesFocusAndBlur()
    {
        var raised = new List<TimeOnly?>();
        var events = new List<string>();
        var cut = RenderBound<TimeOnly?>(raised, null, p => p
            .Add(x => x.OnFocus, (FocusEventArgs _) => events.Add("focus"))
            .Add(x => x.OnBlur, (FocusEventArgs _) => events.Add($"blur {raised.Count}")));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "930p" });
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(21, 30) }));
        Assert.That(events, Is.EqualTo(new[] { "focus", "blur 1" }));
    }

    [Test]
    public async Task OwnRender_KeepsTypedText_OutsideChangeReplacesIt()
    {
        var cut = RenderBound<TimeOnly?>([], new TimeOnly(8, 0));
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "9:4" });

        cut.Render(p => p.Add(x => x.Value, new TimeOnly(8, 0)));
        Assert.That(Text(cut), Is.EqualTo("9:4"));

        cut.Render(p => p.Add(x => x.Value, new TimeOnly(16, 15)));
        Assert.That(Text(cut), Is.EqualTo("4:15 PM"));
    }

    [Test]
    public void Use24HourFalse_TwelveHourInA24HourCulture()
    {
        var cut = RenderBound<TimeOnly?>([], new TimeOnly(14, 30), p => p.Add(x => x.Use24Hour, false), DeDe);

        Assert.That(Text(cut), Does.Contain("2:30").And.Not.Contain("14"));
        Assert.That(cut.Find("input").HasAttribute("inputmode"), Is.False);
    }

    [Test]
    public async Task EscapeWhenClearable_Clears()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, new TimeOnly(7, 0), p => p.Add(x => x.Clearable, true));

        await KeyAsync(cut, "Escape");

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { null }));
    }

    [Test]
    public void Clearable_IgnoredForNonNullable()
    {
        var cut = RenderBound<TimeOnly>([], new TimeOnly(7, 0), p => p.Add(x => x.Clearable, true));

        Assert.That(cut.FindAll(".nx-timepicker-clear"), Is.Empty);
    }

    [Test]
    public async Task ReadOnlyAndDisabled_DoNotOpen()
    {
        var ro = RenderBound<TimeOnly?>([], new TimeOnly(7, 0), p => p.Add(x => x.ReadOnly, true));
        await KeyAsync(ro, "ArrowDown");
        Assert.That(ro.FindAll(".nx-timepicker-popup"), Is.Empty);
        Assert.That(ro.Find("input").HasAttribute("readonly"), Is.True);
        Assert.That(ro.Find(".nx-timepicker-toggle").HasAttribute("disabled"), Is.True);

        var off = RenderBound<TimeOnly?>([], new TimeOnly(7, 0), p => p.Add(x => x.Disabled, true));
        Assert.That(off.Find("input").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task List_SelectedTimeMarked()
    {
        var cut = RenderBound<TimeOnly?>([], new TimeOnly(9, 30));

        await KeyAsync(cut, "ArrowDown");

        var selected = cut.FindAll(".nx-timepicker-popup [role=option][aria-selected=true]");
        Assert.That(selected.Select(o => o.TextContent.Trim()), Is.EqualTo(new[] { "9:30 AM" }));
    }

    // ── Phone sheet ───────────────────────────────────────────────────────────

    [Test]
    public async Task Sheet_Clear_EmptiesAndCloses()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, new TimeOnly(9, 0), p => p.Add(x => x.MobileMode, NxMobileMode.Always).Add(x => x.Clearable, true));

        await cut.Find(".nx-timepicker-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-sheet .nx-timepicker-sheet-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { null }));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
    }

    [Test]
    public async Task Sheet_ListOfButtons_TapPicksAndCloses()
    {
        var raised = new List<TimeOnly?>();
        var cut = RenderBound<TimeOnly?>(raised, null, p => p.Add(x => x.MobileMode, NxMobileMode.Always).Add(x => x.Step, 60));

        await cut.Find(".nx-timepicker-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-sheet button.nx-timepicker-item"), Has.Count.EqualTo(24));
        Assert.That(module.Invocations.Any(i => i.Identifier == "scrollActive" && (bool)i.Arguments[1]!), Is.True);
        await cut.FindAll(".nx-sheet button.nx-timepicker-item")[13].ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new TimeOnly?[] { new TimeOnly(13, 0) }));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public TimeOnly? Time { get; set; }
    }

    [Test]
    public async Task Validation_ChangeMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxTimePicker<TimeOnly?>>(c => c
                .Add(x => x.Culture, EnUs)
                .Add(x => x.Value, model.Time)
                .Add(x => x.ValueChanged, (TimeOnly? v) => model.Time = v)
                .Add(x => x.ValueExpression, () => model.Time)));
        var cut = host.FindComponent<NxTimePicker<TimeOnly?>>();

        await TypeAndCommitAsync(cut, "7:00 AM");
        Assert.That(context.IsModified(context.Field(nameof(Model.Time))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Time)), "Too early.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-timepicker").ClassList, Does.Contain("nx-timepicker-invalid"));
    }
}
