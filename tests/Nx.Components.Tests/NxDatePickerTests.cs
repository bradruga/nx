using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxDatePickerTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");
    private static readonly DateOnly Oct23 = new(2026, 10, 23);
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-picker.js");
        module.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    /// <summary>A bound picker: ValueChanged writes back like @bind-Value. en-US unless overridden.</summary>
    private IRenderedComponent<NxDatePicker<T>> RenderBound<T>(List<T?> raised, T? initial = default,
        Action<ComponentParameterCollectionBuilder<NxDatePicker<T>>>? configure = null, CultureInfo? culture = null, string id = "dp")
    {
        IRenderedComponent<NxDatePicker<T>>? cut = null;
        cut = Render<NxDatePicker<T>>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.Culture, culture ?? EnUs);
            p.Add(x => x.Id, id);
            p.Add(x => x.ValueChanged, (T? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static async Task TypeAndCommitAsync<T>(IRenderedComponent<NxDatePicker<T>> cut, string text)
    {
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
        await cut.Find("input").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });
    }

    private static Task KeyAsync<T>(IRenderedComponent<NxDatePicker<T>> cut, string key, bool alt = false) =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key, AltKey = alt });

    private static string? Text<T>(IRenderedComponent<NxDatePicker<T>> cut) => cut.Find("input").GetAttribute("value");

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_ComboboxInsideSharedFrame_WithPatternPlaceholder()
    {
        var cut = Render<NxDatePicker<DateOnly?>>(p => p.Add(x => x.Label, "Pour date").Add(x => x.Culture, EnUs));

        var input = cut.Find(".nx-field.nx-datepicker .nx-datepicker-field input.nx-datepicker-input");
        Assert.That(input.GetAttribute("role"), Is.EqualTo("combobox"));
        Assert.That(input.GetAttribute("aria-haspopup"), Is.EqualTo("grid"));
        Assert.That(input.GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("mm/dd/yyyy"));
        Assert.That(input.GetAttribute("inputmode"), Is.EqualTo("numeric"));
        Assert.That(cut.Find("label.nx-datepicker-label").GetAttribute("for"), Is.EqualTo(input.Id));
        Assert.That(cut.Find(".nx-datepicker-toggle").GetAttribute("tabindex"), Is.EqualTo("-1"));
    }

    [Test]
    public void Unsupported_Type_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxDatePicker<string>>());
    }

    [Test]
    public void Mask_FollowsCultureOrder()
    {
        RenderBound<DateOnly?>([], Oct23);
        Assert.That(module.Invocations.Any(i => i.Identifier == "mask" && (string?)i.Arguments[1] == "99/99/9999"), Is.True);

        var de = RenderBound<DateOnly?>([], Oct23, culture: DeDe, id: "de");
        Assert.That(module.Invocations.Any(i => i.Identifier == "mask" && (string?)i.Arguments[1] == "99.99.9999"), Is.True);
        Assert.That(de.Find("input").GetAttribute("placeholder"), Is.EqualTo("dd.mm.yyyy"));
    }

    // ── Format ────────────────────────────────────────────────────────────────

    [Test]
    public void Format_AppliesWhileNotFocused()
    {
        var cut = RenderBound<DateOnly?>([], Oct23, p => p.Add(x => x.Format, "D"));

        Assert.That(Text(cut), Is.EqualTo("Friday, October 23, 2026"));
    }

    [Test]
    public async Task Focus_ShowsFixedWidthEditText()
    {
        var cut = RenderBound<DateOnly?>([], new DateOnly(2026, 1, 5), p => p.Add(x => x.Format, "MMM d, yyyy"));
        Assert.That(Text(cut), Is.EqualTo("Jan 5, 2026"));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        Assert.That(Text(cut), Is.EqualTo("01/05/2026"));

        await cut.Find("input").BlurAsync(new FocusEventArgs());
        Assert.That(Text(cut), Is.EqualTo("Jan 5, 2026"));
    }

    [Test]
    public void IsoFormat_EditsInItsOwnOrder()
    {
        RenderBound<DateOnly?>([], Oct23, p => p.Add(x => x.Format, "yyyy-MM-dd"));

        Assert.That(module.Invocations.Any(i => i.Identifier == "mask" && (string?)i.Arguments[1] == "9999-99-99"), Is.True);
    }

    // ── Typing ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Typed_CommitsInCulture()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised);
        await TypeAndCommitAsync(cut, "03/04/2026");

        var de = new List<DateOnly?>();
        var deCut = RenderBound<DateOnly?>(de, null, culture: DeDe, id: "de");
        await TypeAndCommitAsync(deCut, "03.04.2026");

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 3, 4) }));
        Assert.That(de, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 4, 3) }));
    }

    [TestCase("2026-10-23")]
    [TestCase("Oct 23 2026")]
    [TestCase("10/23/26")]
    public async Task Typed_LooseForms_Parse(string text)
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised);

        await TypeAndCommitAsync(cut, text);

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { Oct23 }));
        Assert.That(Text(cut), Is.EqualTo("10/23/2026"));
    }

    [Test]
    public async Task Typed_Garbage_Reverts()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23);

        await TypeAndCommitAsync(cut, "13/45/20");

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("10/23/2026"));
    }

    [Test]
    public async Task Typed_Empty_NullForNullable_RevertsOtherwise()
    {
        var nullable = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(nullable, Oct23);
        await TypeAndCommitAsync(cut, "");

        var plain = new List<DateOnly>();
        var plainCut = RenderBound<DateOnly>(plain, Oct23, id: "plain");
        await TypeAndCommitAsync(plainCut, "");

        Assert.That(nullable, Is.EqualTo(new DateOnly?[] { null }));
        Assert.That(plain, Is.Empty);
        Assert.That(Text(plainCut), Is.EqualTo("10/23/2026"));
    }

    [Test]
    public async Task Typed_OutsideMinMax_MovesToLimit_DisabledDateReverts()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23, p => p
            .Add(x => x.Min, new DateOnly(2026, 10, 5))
            .Add(x => x.Max, new DateOnly(2026, 11, 20))
            .Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Sunday));

        await TypeAndCommitAsync(cut, "01/01/2026");
        await TypeAndCommitAsync(cut, "12/31/2026");
        await TypeAndCommitAsync(cut, "10/25/2026");    // a Sunday

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 20) }));
        Assert.That(Text(cut), Is.EqualTo("11/20/2026"));
    }

    [Test]
    public async Task DateTime_KeepsTimeOfDay()
    {
        var raised = new List<DateTime>();
        var cut = RenderBound(raised, new DateTime(2026, 10, 23, 14, 30, 0, DateTimeKind.Local));

        await TypeAndCommitAsync(cut, "11/02/2026");

        Assert.That(raised.Single(), Is.EqualTo(new DateTime(2026, 11, 2, 14, 30, 0)));
        Assert.That(raised.Single().Kind, Is.EqualTo(DateTimeKind.Local));
    }

    [Test]
    public async Task Enter_CommitsTypedText()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised);

        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "10/23/2026" });
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { Oct23 }));
    }

    // ── Calendar popup ────────────────────────────────────────────────────────

    [Test]
    public async Task Toggle_OpensCalendarAtValue()
    {
        var cut = RenderBound<DateOnly?>([], Oct23);

        await cut.Find(".nx-datepicker-toggle").ClickAsync(new MouseEventArgs());

        var input = cut.Find("input");
        Assert.That(input.GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(input.GetAttribute("aria-controls"), Is.EqualTo("dp-calendar"));
        Assert.That(cut.Find(".nx-datepicker-popup .nx-calendar-title").TextContent.Trim(), Is.EqualTo("October 2026"));
        Assert.That(input.GetAttribute("aria-activedescendant"), Is.EqualTo("dp-calendar-d20261023"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "open"), Is.True);
    }

    [Test]
    public async Task ArrowDown_Opens_ArrowsMoveDay_EnterPicksAndCloses()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23);

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowRight");
        await KeyAsync(cut, "ArrowDown");
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Is.EqualTo("dp-calendar-d20261031"));
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 31) }));
        Assert.That(cut.FindAll(".nx-datepicker-popup"), Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("10/31/2026"));
    }

    [Test]
    public async Task ClickDay_PicksAndCloses()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23);

        await KeyAsync(cut, "ArrowDown", alt: true);
        await cut.Find("td[data-date='2026-10-09']").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 9) }));
        Assert.That(cut.FindAll(".nx-datepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task Typing_WhileOpen_MovesCalendar()
    {
        var cut = RenderBound<DateOnly?>([], Oct23);

        await KeyAsync(cut, "ArrowDown");
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "03/15/2027" });

        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("March 2027"));
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Is.EqualTo("dp-calendar-d20270315"));
    }

    [Test]
    public async Task EscapeAndBlur_Close()
    {
        var cut = RenderBound<DateOnly?>([], Oct23);

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Escape");
        Assert.That(cut.FindAll(".nx-datepicker-popup"), Is.Empty);

        await KeyAsync(cut, "ArrowDown");
        await cut.Find("input").BlurAsync(new FocusEventArgs());
        Assert.That(cut.FindAll(".nx-datepicker-popup"), Is.Empty);
        Assert.That(module.Invocations.Count(i => i.Identifier == "close"), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task ReadOnly_DoesNotOpen()
    {
        var cut = RenderBound<DateOnly?>([], Oct23, p => p.Add(x => x.ReadOnly, true));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll(".nx-datepicker-popup"), Is.Empty);
        Assert.That(cut.Find(".nx-datepicker-toggle").HasAttribute("disabled"), Is.True);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Clearable_ButtonAndEscape_Clear()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23, p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-datepicker-clear").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new DateOnly?[] { null }));
        Assert.That(cut.FindAll(".nx-datepicker-clear"), Is.Empty);

        cut.Render(p => p.Add(x => x.Value, Oct23));
        await KeyAsync(cut, "Escape");
        Assert.That(raised, Is.EqualTo(new DateOnly?[] { null, null }));
    }

    [Test]
    public void Clearable_IgnoredForNonNullable()
    {
        var cut = RenderBound<DateOnly>([], Oct23, p => p.Add(x => x.Clearable, true));

        Assert.That(cut.FindAll(".nx-datepicker-clear"), Is.Empty);
    }

    // ── Phone sheet ───────────────────────────────────────────────────────────

    [Test]
    public async Task MobileAlways_OpensSheet_PickCloses_LocksScroll()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23, p => p
            .Add(x => x.MobileMode, NxMobileMode.Always)
            .Add(x => x.Label, "Inspection"));

        Assert.That(cut.Find("input").GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
        await cut.Find(".nx-datepicker-toggle").ClickAsync(new MouseEventArgs());

        var sheet = cut.Find(".nx-sheet[role=dialog]");
        Assert.That(sheet.GetAttribute("aria-modal"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-sheet-title").TextContent, Is.EqualTo("Inspection"));
        Assert.That(cut.Find(".nx-sheet .nx-calendar-grids").GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "lockScroll" && (bool)i.Arguments[0]!), Is.True);

        await cut.Find(".nx-sheet td[data-date='2026-10-30']").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 30) }));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "lockScroll" && !(bool)i.Arguments[0]!), Is.True);
    }

    [Test]
    public async Task Sheet_EscapeAndBack_CloseWithoutChange()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23, p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        await cut.Find(".nx-datepicker-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-sheet").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);

        await cut.Find(".nx-datepicker-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-sheet-close").ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task Sheet_Clear()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound<DateOnly?>(raised, Oct23, p => p
            .Add(x => x.MobileMode, NxMobileMode.Always)
            .Add(x => x.Clearable, true));

        await cut.Find(".nx-datepicker-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-datepicker-sheet-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { null }));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
    }

    [Test]
    public void ViewportChange_SwitchesToSheet()
    {
        var cut = RenderBound<DateOnly?>([], Oct23);

        cut.Instance.OnViewportChanged(true);

        cut.WaitForState(() => cut.Find("input").GetAttribute("aria-haspopup") == "dialog");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public DateOnly? Date { get; set; }
    }

    [Test]
    public async Task Validation_ChangeMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxDatePicker<DateOnly?>>(c => c
                .Add(x => x.Culture, EnUs)
                .Add(x => x.Value, model.Date)
                .Add(x => x.ValueChanged, (DateOnly? v) => model.Date = v)
                .Add(x => x.ValueExpression, () => model.Date)));
        var cut = host.FindComponent<NxDatePicker<DateOnly?>>();

        await TypeAndCommitAsync(cut, "10/23/2026");
        Assert.That(context.IsModified(context.Field(nameof(Model.Date))), Is.True);
        Assert.That(cut.Find("input").ClassList, Does.Contain("modified"));

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Date)), "Too late.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-datepicker").ClassList, Does.Contain("nx-datepicker-invalid"));
    }
}
