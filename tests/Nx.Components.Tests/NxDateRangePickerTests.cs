using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxDateRangePickerTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-picker.js");
        module.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    private sealed class Bound
    {
        public DateOnly? Start, End;
        public List<string> Log = [];
    }

    private IRenderedComponent<NxDateRangePicker<DateOnly?>> RenderBound(Bound bound,
        Action<ComponentParameterCollectionBuilder<NxDateRangePicker<DateOnly?>>>? configure = null)
    {
        IRenderedComponent<NxDateRangePicker<DateOnly?>>? cut = null;
        cut = Render<NxDateRangePicker<DateOnly?>>(p =>
        {
            p.Add(x => x.StartValue, bound.Start);
            p.Add(x => x.EndValue, bound.End);
            p.Add(x => x.Culture, EnUs);
            p.Add(x => x.Id, "r");
            p.Add(x => x.StartValueChanged, (DateOnly? v) =>
            {
                bound.Start = v;
                bound.Log.Add($"start {v:yyyy-MM-dd}");
                cut?.Render(q => q.Add(x => x.StartValue, v));
            });
            p.Add(x => x.EndValueChanged, (DateOnly? v) =>
            {
                bound.End = v;
                bound.Log.Add($"end {v:yyyy-MM-dd}");
                cut?.Render(q => q.Add(x => x.EndValue, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task KeyAsync(IRenderedComponent<NxDateRangePicker<DateOnly?>> cut, string inputId, string key, bool shift = false) =>
        cut.Find($"#{inputId}").KeyDownAsync(new KeyboardEventArgs { Key = key, ShiftKey = shift });

    private static async Task TypeAndCommitAsync(IRenderedComponent<NxDateRangePicker<DateOnly?>> cut, string inputId, string text)
    {
        await cut.Find($"#{inputId}").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
        await cut.Find($"#{inputId}").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });
    }

    private static Task PickAsync(IRenderedComponent<NxDateRangePicker<DateOnly?>> cut, string date) =>
        cut.Find($".nx-daterange-popup td[data-date='{date}']").ClickAsync(new MouseEventArgs());

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_TwoLabelledFieldsInAGroup()
    {
        var cut = RenderBound(new Bound(), p => p.Add(x => x.Label, "Pour window"));

        var group = cut.Find(".nx-daterangepicker[role=group]");
        Assert.That(group.GetAttribute("aria-labelledby"), Is.EqualTo("r-label"));
        Assert.That(cut.Find("#r-label").TextContent, Is.EqualTo("Pour window"));
        Assert.That(cut.Find("label[for=r-start]").TextContent, Is.EqualTo("Start"));
        Assert.That(cut.Find("label[for=r-end]").TextContent, Is.EqualTo("End"));
        Assert.That(cut.Find("#r-start").GetAttribute("role"), Is.EqualTo("combobox"));
    }

    [Test]
    public async Task Open_ShowsTwoMonthsWithRange()
    {
        var cut = RenderBound(new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) });

        await KeyAsync(cut, "r-start", "ArrowDown");

        Assert.That(cut.FindAll(".nx-daterange-popup table.nx-calendar-month"), Has.Count.EqualTo(2));
        Assert.That(cut.Find("td[data-date='2026-10-21']").ClassList, Does.Contain("nx-calendar-in-range"));
        Assert.That(cut.Find("#r-start").GetAttribute("aria-activedescendant"), Is.EqualTo("r-calendar-d20261019"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "open" && (string?)i.Arguments[5] == "OnOutsidePointerDown"), Is.True);
    }

    [Test]
    public void Months_One()
    {
        var cut = RenderBound(new Bound(), p => p.Add(x => x.Months, 1));

        cut.Find(".nx-daterange-toggle").Click();

        Assert.That(cut.FindAll(".nx-daterange-popup table.nx-calendar-month"), Has.Count.EqualTo(1));
    }

    // ── Picking ───────────────────────────────────────────────────────────────

    [Test]
    public async Task FirstPickStart_SecondPickEnd_Closes()
    {
        var bound = new Bound();
        var cut = RenderBound(bound);

        await KeyAsync(cut, "r-start", "ArrowDown");
        await PickAsync(cut, "2026-11-02");
        Assert.That(cut.FindAll(".nx-daterange-popup"), Has.Count.EqualTo(1));
        await PickAsync(cut, "2026-11-06");

        Assert.That(bound.Log, Is.EqualTo(new[] { "start 2026-11-02", "end 2026-11-06" }));
        Assert.That(cut.FindAll(".nx-daterange-popup"), Is.Empty);
    }

    [Test]
    public async Task PickBeforeStart_RestartsFromThere()
    {
        var bound = new Bound();
        var cut = RenderBound(bound);

        await KeyAsync(cut, "r-start", "ArrowDown");
        await PickAsync(cut, "2026-11-10");
        await PickAsync(cut, "2026-11-03");
        await PickAsync(cut, "2026-11-05");

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(2026, 11, 3)));
        Assert.That(bound.End, Is.EqualTo(new DateOnly(2026, 11, 5)));
    }

    [Test]
    public async Task NewStartAfterEnd_ClearsEnd()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound);

        await KeyAsync(cut, "r-start", "ArrowDown");
        await PickAsync(cut, "2026-10-28");

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(2026, 10, 28)));
        Assert.That(bound.End, Is.Null);
    }

    [Test]
    public async Task OpenFromEnd_PicksTheEnd()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound);

        await cut.Find("#r-end").FocusAsync(new FocusEventArgs());
        await KeyAsync(cut, "r-end", "ArrowDown");
        await PickAsync(cut, "2026-10-30");

        Assert.That(bound.Log, Is.EqualTo(new[] { "end 2026-10-30" }));
    }

    [Test]
    public async Task Keyboard_EnterPicksStartThenEnd()
    {
        var bound = new Bound { Start = new(2026, 10, 19) };
        var cut = RenderBound(bound);

        await cut.Find("#r-start").FocusAsync(new FocusEventArgs());
        await KeyAsync(cut, "r-start", "ArrowDown");
        await KeyAsync(cut, "r-start", "Enter");
        await KeyAsync(cut, "r-start", "ArrowRight");
        await KeyAsync(cut, "r-start", "ArrowRight");
        await KeyAsync(cut, "r-start", "Enter");

        Assert.That(bound.End, Is.EqualTo(new DateOnly(2026, 10, 21)));
        Assert.That(cut.FindAll(".nx-daterange-popup"), Is.Empty);
    }

    // ── Closing ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Tab_BetweenFieldsKeepsOpen_OutOfPairCloses()
    {
        var cut = RenderBound(new Bound());

        await KeyAsync(cut, "r-start", "ArrowDown");
        await KeyAsync(cut, "r-start", "Tab");
        Assert.That(cut.FindAll(".nx-daterange-popup"), Has.Count.EqualTo(1));
        await KeyAsync(cut, "r-end", "Tab", shift: true);
        Assert.That(cut.FindAll(".nx-daterange-popup"), Has.Count.EqualTo(1));
        await KeyAsync(cut, "r-end", "Tab");
        Assert.That(cut.FindAll(".nx-daterange-popup"), Is.Empty);
    }

    [Test]
    public async Task OutsidePointerDown_Closes()
    {
        var cut = RenderBound(new Bound());

        await KeyAsync(cut, "r-start", "ArrowDown");
        cut.Instance.OnOutsidePointerDown();

        cut.WaitForState(() => cut.FindAll(".nx-daterange-popup").Count == 0);
    }

    // ── Typing ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Typed_EachEnd()
    {
        var bound = new Bound();
        var cut = RenderBound(bound);

        await TypeAndCommitAsync(cut, "r-start", "10/19/2026");
        await TypeAndCommitAsync(cut, "r-end", "10/23/2026");

        Assert.That(bound.Log, Is.EqualTo(new[] { "start 2026-10-19", "end 2026-10-23" }));
    }

    [Test]
    public async Task Typed_StartAfterEnd_Swaps()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound);

        await TypeAndCommitAsync(cut, "r-start", "10/30/2026");

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(2026, 10, 23)));
        Assert.That(bound.End, Is.EqualTo(new DateOnly(2026, 10, 30)));
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("10/23/2026"));
    }

    [Test]
    public async Task Clearable_ClearsOneEnd()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound, p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-daterange-end .nx-daterange-clear").ClickAsync(new MouseEventArgs());

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(2026, 10, 19)));
        Assert.That(bound.End, Is.Null);
    }

    // ── Phone sheet ───────────────────────────────────────────────────────────

    [Test]
    public async Task Sheet_OneMonth_WalksStartThenEnd()
    {
        var bound = new Bound();
        var cut = RenderBound(bound, p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        await cut.Find(".nx-daterange-start .nx-daterange-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-sheet table.nx-calendar-month"), Has.Count.EqualTo(1));
        Assert.That(cut.Find(".nx-daterange-sheet-hint").TextContent, Is.EqualTo("Pick the start date"));
        var start = DateOnly.ParseExact(cut.Find(".nx-sheet td[data-date]:not(.nx-calendar-other)").GetAttribute("data-date")!, "yyyy-MM-dd");
        await cut.Find($".nx-sheet td[data-date='{start:yyyy-MM-dd}']").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find(".nx-daterange-sheet-hint").TextContent, Is.EqualTo("Pick the end date"));
        await cut.Find($".nx-sheet td[data-date='{start.AddDays(3):yyyy-MM-dd}']").ClickAsync(new MouseEventArgs());

        Assert.That(bound.Start, Is.EqualTo(start));
        Assert.That(bound.End, Is.EqualTo(start.AddDays(3)));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
    }

    [Test]
    public async Task Sheet_SummaryShowsTheRange_ClearEmptiesBoth()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound, p => p.Add(x => x.MobileMode, NxMobileMode.Always).Add(x => x.Clearable, true));

        await cut.Find(".nx-daterange-start .nx-daterange-toggle").ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".nx-daterange-sheet-summary span").Select(s => s.TextContent),
            Is.EqualTo(new[] { "10/19/2026", "–", "10/23/2026" }));

        await cut.Find(".nx-daterange-sheet-clear").ClickAsync(new MouseEventArgs());

        Assert.That(bound.Start, Is.Null);
        Assert.That(bound.End, Is.Null);
        Assert.That(cut.FindAll(".nx-daterange-sheet-summary span").Select(s => s.TextContent),
            Is.EqualTo(new[] { "Start", "–", "End" }));
    }

    [Test]
    public async Task Sheet_Done_ClosesWithoutChange()
    {
        var bound = new Bound { Start = new(2026, 10, 19) };
        var cut = RenderBound(bound, p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        await cut.Find(".nx-daterange-start .nx-daterange-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-daterange-sheet-done").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
        Assert.That(bound.Log, Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "lockScroll" && !(bool)i.Arguments[0]!), Is.True);
    }

    [Test]
    public void ViewportChange_SwitchesToSheet()
    {
        var cut = RenderBound(new Bound());

        cut.Instance.OnViewportChanged(true);

        cut.WaitForState(() => cut.Find("#r-start").GetAttribute("aria-haspopup") == "dialog");
    }

    // ── Typing (more) ─────────────────────────────────────────────────────────

    [Test]
    public async Task Typed_EndBeforeStart_Swaps()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound);

        await TypeAndCommitAsync(cut, "r-end", "10/10/2026");

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(2026, 10, 10)));
        Assert.That(bound.End, Is.EqualTo(new DateOnly(2026, 10, 19)));
        Assert.That(cut.Find("#r-end").GetAttribute("value"), Is.EqualTo("10/19/2026"));
    }

    [Test]
    public async Task Typed_ShortForm_IsThisYear()
    {
        var bound = new Bound();
        var cut = RenderBound(bound);

        await TypeAndCommitAsync(cut, "r-start", "12/5");

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(DateTime.Today.Year, 12, 5)));
    }

    [Test]
    public async Task Typed_OutsideMinMax_MovesToLimit()
    {
        var bound = new Bound();
        var cut = RenderBound(bound, p => p
            .Add(x => x.Min, new DateOnly(2026, 10, 5))
            .Add(x => x.Max, new DateOnly(2026, 11, 20)));

        await TypeAndCommitAsync(cut, "r-start", "01/01/2026");
        await TypeAndCommitAsync(cut, "r-end", "12/31/2026");

        Assert.That(bound.Start, Is.EqualTo(new DateOnly(2026, 10, 5)));
        Assert.That(bound.End, Is.EqualTo(new DateOnly(2026, 11, 20)));
    }

    [Test]
    public async Task Typed_DisabledDate_Reverts()
    {
        var bound = new Bound { Start = new(2026, 10, 19) };
        var cut = RenderBound(bound, p => p.Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Sunday));

        await TypeAndCommitAsync(cut, "r-start", "10/25/2026");     // a Sunday

        Assert.That(bound.Log, Is.Empty);
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("10/19/2026"));
    }

    [Test]
    public async Task Typed_Garbage_Reverts()
    {
        var bound = new Bound { Start = new(2026, 10, 19) };
        var cut = RenderBound(bound);

        await TypeAndCommitAsync(cut, "r-start", "13/45/20");

        Assert.That(bound.Log, Is.Empty);
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("10/19/2026"));
    }

    [Test]
    public async Task Typed_Empty_RevertsForNonNullable()
    {
        var raised = new List<DateOnly>();
        var cut = Render<NxDateRangePicker<DateOnly>>(p => p
            .Add(x => x.StartValue, new DateOnly(2026, 10, 19))
            .Add(x => x.EndValue, new DateOnly(2026, 10, 23))
            .Add(x => x.Culture, EnUs)
            .Add(x => x.Id, "n")
            .Add(x => x.StartValueChanged, (DateOnly v) => raised.Add(v)));

        await cut.Find("#n-start").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "" });
        await cut.Find("#n-start").TriggerEventAsync("onchange", new ChangeEventArgs { Value = "" });

        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find("#n-start").GetAttribute("value"), Is.EqualTo("10/19/2026"));
    }

    [Test]
    public async Task DateTime_EachEndKeepsItsTimeOfDay()
    {
        var ends = new List<DateTime>();
        var cut = Render<NxDateRangePicker<DateTime>>(p => p
            .Add(x => x.StartValue, new DateTime(2026, 10, 19, 8, 0, 0))
            .Add(x => x.EndValue, new DateTime(2026, 10, 23, 17, 30, 0))
            .Add(x => x.Culture, EnUs)
            .Add(x => x.Id, "dt")
            .Add(x => x.EndValueChanged, (DateTime v) => ends.Add(v)));

        await cut.Find("#dt-end").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "10/25/2026" });
        await cut.Find("#dt-end").TriggerEventAsync("onchange", new ChangeEventArgs { Value = "10/25/2026" });

        Assert.That(ends, Is.EqualTo(new[] { new DateTime(2026, 10, 25, 17, 30, 0) }));
    }

    [Test]
    public void StartAndEndLabels()
    {
        var cut = RenderBound(new Bound(), p => p.Add(x => x.StartLabel, "Check in").Add(x => x.EndLabel, "Check out"));

        Assert.That(cut.Find("label[for=r-start]").TextContent, Is.EqualTo("Check in"));
        Assert.That(cut.Find("label[for=r-end]").TextContent, Is.EqualTo("Check out"));
        Assert.That(cut.Find("#r-start").GetAttribute("placeholder"), Is.EqualTo("mm/dd/yyyy"));
    }

    [Test]
    public async Task Format_ShowsWhileNotFocused_EditTextSwappedInByTheBrowserOnFocus()
    {
        var cut = RenderBound(new Bound { Start = new(2026, 10, 19) }, p => p.Add(x => x.Format, "MMM d, yyyy"));
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("Oct 19, 2026"));
        Assert.That(cut.Find("#r-start").GetAttribute("data-nx-edit"), Is.EqualTo("10/19/2026"));
        Assert.That(cut.Find("#r-end").GetAttribute("data-nx-edit"), Is.EqualTo(""));
        Assert.That(module.Invocations.Count(i => i.Identifier == "editOnFocus"), Is.EqualTo(2), "one per field");

        // A swap rendered from here would land a round trip late; nx-input.js swaps and raises input.
        await cut.Find("#r-start").FocusAsync(new FocusEventArgs());
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("Oct 19, 2026"));
        await cut.Find("#r-start").InputAsync(new ChangeEventArgs { Value = "10/19/2026" });
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("10/19/2026"));

        await cut.Find("#r-start").BlurAsync(new FocusEventArgs());
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("Oct 19, 2026"));
    }

    [Test]
    public async Task OutsideChange_ReplacesText_OwnRenderKeepsTyping()
    {
        var cut = RenderBound(new Bound { Start = new(2026, 10, 19) });
        await cut.Find("#r-start").FocusAsync(new FocusEventArgs());
        await cut.Find("#r-start").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "11/0" });

        cut.Render(p => p.Add(x => x.StartValue, new DateOnly(2026, 10, 19)));
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("11/0"));

        cut.Render(p => p.Add(x => x.StartValue, new DateOnly(2026, 12, 1)));
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("12/01/2026"));
    }

    [Test]
    public async Task Typing_WhileOpen_MovesCalendar()
    {
        var cut = RenderBound(new Bound { Start = new(2026, 10, 19) });

        await KeyAsync(cut, "r-start", "ArrowDown");
        await cut.Find("#r-start").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "03/15/2027" });

        Assert.That(cut.Find("#r-start").GetAttribute("aria-activedescendant"), Is.EqualTo("r-calendar-d20270315"));
        Assert.That(cut.FindAll("caption").Select(c => c.TextContent), Does.Contain("March 2027"));
    }

    [Test]
    public async Task Escape_WhenClearable_ClearsThatEnd()
    {
        var bound = new Bound { Start = new(2026, 10, 19), End = new(2026, 10, 23) };
        var cut = RenderBound(bound, p => p.Add(x => x.Clearable, true));

        await KeyAsync(cut, "r-start", "Escape");

        Assert.That(bound.Log, Is.EqualTo(new[] { "start " }));
        Assert.That(bound.End, Is.EqualTo(new DateOnly(2026, 10, 23)));
    }

    // ── Popup (more) ──────────────────────────────────────────────────────────

    [Test]
    public async Task EscapeAndAltUp_ClosePopup_KeepValues()
    {
        var bound = new Bound { Start = new(2026, 10, 19) };
        var cut = RenderBound(bound);

        await KeyAsync(cut, "r-start", "ArrowDown");
        await KeyAsync(cut, "r-start", "Escape");
        Assert.That(cut.FindAll(".nx-daterange-popup"), Is.Empty);

        await KeyAsync(cut, "r-start", "ArrowDown");
        await cut.Find("#r-start").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });
        Assert.That(cut.FindAll(".nx-daterange-popup"), Is.Empty);
        Assert.That(bound.Log, Is.Empty);
    }

    [Test]
    public async Task Calendar_LimitsDisableDays_NotPickable()
    {
        var bound = new Bound { Start = new(2026, 10, 19) };
        var cut = RenderBound(bound, p => p
            .Add(x => x.Min, new DateOnly(2026, 10, 10))
            .Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Sunday));

        await KeyAsync(cut, "r-start", "ArrowDown");
        Assert.That(cut.Find("td[data-date='2026-10-09']").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(cut.Find("td[data-date='2026-10-25']").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        await PickAsync(cut, "2026-10-09");
        await PickAsync(cut, "2026-10-25");

        Assert.That(bound.Log, Is.Empty);
    }

    [Test]
    public async Task WeekNumbersAndFirstDayOfWeek_ReachTheCalendar()
    {
        var cut = RenderBound(new Bound { Start = new(2026, 10, 19) }, p => p
            .Add(x => x.ShowWeekNumbers, true)
            .Add(x => x.FirstDayOfWeek, DayOfWeek.Monday));

        await KeyAsync(cut, "r-start", "ArrowDown");

        Assert.That(cut.FindAll(".nx-daterange-popup th.nx-calendar-week"), Is.Not.Empty);
        Assert.That(cut.FindAll(".nx-daterange-popup th.nx-calendar-weekday:not(.nx-calendar-week-header)")[0].GetAttribute("abbr"), Is.EqualTo("Monday"));
    }

    [Test]
    public async Task ReadOnly_DoesNotOpen()
    {
        var cut = RenderBound(new Bound(), p => p.Add(x => x.ReadOnly, true));

        await KeyAsync(cut, "r-start", "ArrowDown");

        Assert.That(cut.FindAll(".nx-daterange-popup"), Is.Empty);
        Assert.That(cut.Find("#r-start").HasAttribute("readonly"), Is.True);
        Assert.That(cut.FindAll(".nx-daterange-toggle").All(t => t.HasAttribute("disabled")), Is.True);
    }

    [Test]
    public void Disabled_BothFieldsAndToggles()
    {
        var cut = RenderBound(new Bound(), p => p.Add(x => x.Disabled, true));

        Assert.That(cut.Find("#r-start").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("#r-end").HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll(".nx-daterange-toggle").All(t => t.HasAttribute("disabled")), Is.True);
    }

    [Test]
    public void Unsupported_Type_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxDateRangePicker<string>>());
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
    }

    [Test]
    public async Task Validation_EachEndIsItsOwnField()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxDateRangePicker<DateOnly?>>(c => c
                .Add(x => x.Culture, EnUs)
                .Add(x => x.Id, "v")
                .Add(x => x.StartValue, model.From)
                .Add(x => x.StartValueChanged, (DateOnly? v) => model.From = v)
                .Add(x => x.StartValueExpression, () => model.From)
                .Add(x => x.EndValue, model.To)
                .Add(x => x.EndValueChanged, (DateOnly? v) => model.To = v)
                .Add(x => x.EndValueExpression, () => model.To)));
        var cut = host.FindComponent<NxDateRangePicker<DateOnly?>>();

        await TypeAndCommitAsync(cut, "v-start", "10/19/2026");
        Assert.That(context.IsModified(context.Field(nameof(Model.From))), Is.True);
        Assert.That(context.IsModified(context.Field(nameof(Model.To))), Is.False);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.To)), "Pick the last day.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("#v-end").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find("#v-start").HasAttribute("aria-invalid"), Is.False);
    }

    // ── DateTimeOffset ────────────────────────────────────────────────────────

    [Test]
    public async Task DateTimeOffset_StartKeepsItsOffset_EmptyEndTakesTheBrowsers()
    {
        module.Setup<BrowserZone>("timeZone").SetResult(new BrowserZone("America/Chicago", -360));
        DateTimeOffset? start = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.FromHours(2)), end = null;
        IRenderedComponent<NxDateRangePicker<DateTimeOffset?>>? cut = null;
        cut = Render<NxDateRangePicker<DateTimeOffset?>>(p => p
            .Add(x => x.Culture, EnUs)
            .Add(x => x.Id, "r")
            .Add(x => x.StartValue, start)
            .Add(x => x.EndValue, end)
            .Add(x => x.StartValueChanged, (DateTimeOffset? v) => { start = v; cut?.Render(q => q.Add(x => x.StartValue, v)); })
            .Add(x => x.EndValueChanged, (DateTimeOffset? v) => { end = v; cut?.Render(q => q.Add(x => x.EndValue, v)); }));
        Assert.That(cut.Find("#r-start").GetAttribute("value"), Is.EqualTo("10/19/2026"));

        foreach (var (id, text) in new[] { ("r-start", "10/20/2026"), ("r-end", "12/1/2026") })
        {
            await cut.Find($"#{id}").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
            await cut.Find($"#{id}").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });
        }

        Assert.That(start, Is.EqualTo(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.FromHours(2))));
        Assert.That(start!.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(end, Is.EqualTo(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(-6))));
        Assert.That(end!.Value.Offset, Is.EqualTo(TimeSpan.FromHours(-6)), "standard time in December");
    }
}
