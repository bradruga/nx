using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxCalendarTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");
    private static readonly DateOnly Oct23 = new(2026, 10, 23);

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxCalendar> RenderBound(List<DateOnly?> raised, DateOnly? initial = null,
        Action<ComponentParameterCollectionBuilder<NxCalendar>>? configure = null, CultureInfo? culture = null)
    {
        IRenderedComponent<NxCalendar>? cut = null;
        cut = Render<NxCalendar>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.Culture, culture ?? EnUs);
            p.Add(x => x.Id, "cal");
            p.Add(x => x.ValueChanged, (DateOnly? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task KeyAsync(IRenderedComponent<NxCalendar> cut, string key, bool shift = false, bool ctrl = false) =>
        cut.Find(".nx-calendar-grids").KeyDownAsync(new KeyboardEventArgs { Key = key, ShiftKey = shift, CtrlKey = ctrl });

    private static string? Active(IRenderedComponent<NxCalendar> cut) => cut.Find(".nx-calendar-grids").GetAttribute("aria-activedescendant");

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Month_SixWeeksStartingOnCultureFirstDay()
    {
        var cut = RenderBound([], Oct23);

        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("October 2026"));
        Assert.That(cut.FindAll("td[role=gridcell]"), Has.Count.EqualTo(42));
        Assert.That(cut.FindAll("th.nx-calendar-weekday")[0].GetAttribute("abbr"), Is.EqualTo("Sunday"));
        Assert.That(cut.FindAll("td[role=gridcell]")[0].GetAttribute("data-date"), Is.EqualTo("2026-09-27"));
        Assert.That(cut.FindAll("td[role=gridcell]")[0].ClassList, Does.Contain("nx-calendar-other"));
    }

    [Test]
    public void FirstDayOfWeek_OverridesCulture()
    {
        var cut = RenderBound([], Oct23, p => p.Add(x => x.FirstDayOfWeek, DayOfWeek.Monday));

        Assert.That(cut.FindAll("th.nx-calendar-weekday")[0].GetAttribute("abbr"), Is.EqualTo("Monday"));
        Assert.That(cut.FindAll("td[role=gridcell]")[0].GetAttribute("data-date"), Is.EqualTo("2026-09-28"));
    }

    [Test]
    public void Selected_TodayAndActive_Marked()
    {
        var cut = RenderBound([], Oct23);

        var cell = cut.Find("#cal-d20261023");
        Assert.That(cell.GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cell.ClassList, Does.Contain("nx-calendar-selected"));
        Assert.That(Active(cut), Is.EqualTo("cal-d20261023"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        cut.Render(p => p.Add(x => x.Value, today));
        Assert.That(cut.Find($"#cal-d{today:yyyyMMdd}").GetAttribute("aria-current"), Is.EqualTo("date"));
    }

    [Test]
    public void WeekNumbers_IsoInGermany()
    {
        var cut = RenderBound([], new DateOnly(2026, 1, 15), p => p.Add(x => x.ShowWeekNumbers, true), DeDe);

        // 2026-01-01 is a Thursday: the week of Dec 29 – Jan 4 is ISO week 1.
        var weeks = cut.FindAll("th.nx-calendar-week").Select(w => w.TextContent.Trim()).ToList();
        Assert.That(weeks.Take(3), Is.EqualTo(new[] { "1", "2", "3" }));
    }

    [Test]
    public void WeekNumbers_UsCountsTheWeekOfJanuaryFirst()
    {
        var cut = RenderBound([], new DateOnly(2027, 1, 15), p => p.Add(x => x.ShowWeekNumbers, true));

        // 2027-01-01 is a Friday; its Sunday-start week is week 1 in the US rule.
        Assert.That(cut.FindAll("th.nx-calendar-week")[0].TextContent.Trim(), Is.EqualTo("1"));
    }

    [Test]
    public void TwoMonths_SideBySide_WithoutOtherMonthDays()
    {
        var cut = RenderBound([], Oct23, p => p.Add(x => x.Months, 2));

        Assert.That(cut.FindAll("table.nx-calendar-month"), Has.Count.EqualTo(2));
        Assert.That(cut.FindAll("caption").Select(c => c.TextContent), Is.EqualTo(new[] { "October 2026", "November 2026" }));
        Assert.That(cut.FindAll(".nx-calendar-other"), Is.Empty);
        Assert.That(cut.FindAll("td[role=gridcell]"), Has.Count.EqualTo(31 + 30));
    }

    [Test]
    public void Range_HighlightsStartBandAndEnd()
    {
        var cut = RenderBound([], null, p =>
        {
            p.Add(x => x.RangeStart, new DateOnly(2026, 10, 19));
            p.Add(x => x.RangeEnd, Oct23);
        });

        Assert.That(cut.Find("#cal-d20261019").ClassList, Does.Contain("nx-calendar-range-start"));
        Assert.That(cut.Find("#cal-d20261021").ClassList, Does.Contain("nx-calendar-in-range"));
        Assert.That(cut.Find("#cal-d20261023").ClassList, Does.Contain("nx-calendar-range-end"));
        Assert.That(cut.Find("#cal-d20261024").ClassList, Does.Not.Contain("nx-calendar-in-range"));
    }

    // ── Picking ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Click_PicksDay()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, Oct23);

        await cut.Find("#cal-d20261014").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 14) }));
        Assert.That(cut.Find("#cal-d20261014").GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public async Task MinMaxAndCallback_DisableDays()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, Oct23, p =>
        {
            p.Add(x => x.Min, new DateOnly(2026, 10, 10));
            p.Add(x => x.Max, new DateOnly(2026, 10, 28));
            p.Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Sunday);
        });

        Assert.That(cut.Find("#cal-d20261009").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(cut.Find("#cal-d20261029").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(cut.Find("#cal-d20261018").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        await cut.Find("#cal-d20261018").ClickAsync(new MouseEventArgs());
        await cut.Find("#cal-d20261009").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find(".nx-calendar-prev").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-calendar-next").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task TodayButton_PicksToday()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, new DateOnly(2020, 1, 1));

        await cut.Find(".nx-calendar-today-button").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { DateOnly.FromDateTime(DateTime.Today) }));
    }

    [Test]
    public async Task PrevNext_ChangeMonth()
    {
        var cut = RenderBound([], Oct23);

        await cut.Find(".nx-calendar-next").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("November 2026"));
        await cut.Find(".nx-calendar-prev").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-calendar-prev").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("September 2026"));
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Arrows_MoveByDayAndWeek()
    {
        var cut = RenderBound([], Oct23);

        await KeyAsync(cut, "ArrowRight");
        Assert.That(Active(cut), Is.EqualTo("cal-d20261024"));
        await KeyAsync(cut, "ArrowDown");
        Assert.That(Active(cut), Is.EqualTo("cal-d20261031"));
        await KeyAsync(cut, "ArrowDown");
        Assert.That(Active(cut), Is.EqualTo("cal-d20261107"));
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("November 2026"));
        await KeyAsync(cut, "ArrowLeft");
        await KeyAsync(cut, "ArrowUp");
        Assert.That(Active(cut), Is.EqualTo("cal-d20261030"));
    }

    [Test]
    public async Task PageKeys_MoveMonthOrYear_HomeEndWeek()
    {
        var cut = RenderBound([], Oct23);

        await KeyAsync(cut, "PageDown");
        Assert.That(Active(cut), Is.EqualTo("cal-d20261123"));
        await KeyAsync(cut, "PageDown", shift: true);
        Assert.That(Active(cut), Is.EqualTo("cal-d20271123"));
        await KeyAsync(cut, "Home");
        Assert.That(Active(cut), Is.EqualTo("cal-d20271121"));   // Sunday
        await KeyAsync(cut, "End");
        Assert.That(Active(cut), Is.EqualTo("cal-d20271127"));   // Saturday
    }

    [Test]
    public async Task PageUp_ClampsDayToShorterMonth()
    {
        var cut = RenderBound([], new DateOnly(2026, 3, 31));

        await KeyAsync(cut, "PageUp");

        Assert.That(Active(cut), Is.EqualTo("cal-d20260228"));
    }

    [Test]
    public async Task Arrows_StopAtMin()
    {
        var cut = RenderBound([], new DateOnly(2026, 10, 11), p => p.Add(x => x.Min, new DateOnly(2026, 10, 10)));

        await KeyAsync(cut, "ArrowLeft");
        await KeyAsync(cut, "ArrowLeft");
        await KeyAsync(cut, "ArrowUp");

        Assert.That(Active(cut), Is.EqualTo("cal-d20261010"));
    }

    [Test]
    public async Task Enter_PicksActive_ButNotADisabledDay()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, Oct23, p => p.Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Saturday));

        await KeyAsync(cut, "ArrowRight");     // Saturday 24th
        await KeyAsync(cut, "Enter");
        Assert.That(raised, Is.Empty);
        await KeyAsync(cut, "ArrowRight");
        await KeyAsync(cut, " ");
        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 25) }));
    }

    [Test]
    public async Task CtrlUp_ZoomsToYearThenDecade_EnterZoomsBackIn()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, Oct23);

        await KeyAsync(cut, "ArrowUp", ctrl: true);
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("2026"));
        Assert.That(cut.FindAll("table.nx-calendar-year td[role=gridcell]"), Has.Count.EqualTo(12));
        Assert.That(Active(cut), Is.EqualTo("cal-m202610"));

        await KeyAsync(cut, "ArrowUp", ctrl: true);
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("2020–2029"));
        Assert.That(Active(cut), Is.EqualTo("cal-y2026"));
        await KeyAsync(cut, "ArrowRight");
        await KeyAsync(cut, "Enter");          // 2027 → year view
        await KeyAsync(cut, "ArrowDown");      // four months on
        Assert.That(Active(cut), Is.EqualTo("cal-m202802"));
        await KeyAsync(cut, "Enter");          // month view
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("February 2028"));
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task TitleClick_ZoomsOut_MonthClickZoomsIn()
    {
        var cut = RenderBound([], Oct23);

        await cut.Find(".nx-calendar-title").ClickAsync(new MouseEventArgs());
        await cut.Find("#cal-m202603").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("March 2026"));
        Assert.That(Active(cut), Is.EqualTo("cal-d20260323"));
    }

    [Test]
    public async Task CtrlDown_ZoomsBackIn()
    {
        var cut = RenderBound([], Oct23);

        await KeyAsync(cut, "ArrowUp", ctrl: true);
        await KeyAsync(cut, "ArrowUp", ctrl: true);
        await KeyAsync(cut, "ArrowDown", ctrl: true);
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("2026"));
        await KeyAsync(cut, "ArrowDown", ctrl: true);

        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("October 2026"));
        Assert.That(Active(cut), Is.EqualTo("cal-d20261023"));
    }

    [Test]
    public async Task YearAndDecadeViews_DisableMonthsAndYearsOutsideMinMax()
    {
        var cut = RenderBound([], Oct23, p => p
            .Add(x => x.Min, new DateOnly(2026, 3, 15))
            .Add(x => x.Max, new DateOnly(2027, 6, 1)));

        await KeyAsync(cut, "ArrowUp", ctrl: true);
        Assert.That(cut.Find("#cal-m202602").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(cut.Find("#cal-m202603").HasAttribute("aria-disabled"), Is.False);

        await KeyAsync(cut, "ArrowUp", ctrl: true);
        Assert.That(cut.Find("#cal-y2025").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(cut.Find("#cal-y2027").HasAttribute("aria-disabled"), Is.False);
        Assert.That(cut.Find("#cal-y2028").GetAttribute("aria-disabled"), Is.EqualTo("true"));
    }

    [Test]
    public void NoValue_OpensOnToday()
    {
        var cut = RenderBound([]);

        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.That(Active(cut), Is.EqualTo($"cal-d{today:yyyyMMdd}"));
        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo(today.ToString("MMMM yyyy", EnUs)));
    }

    [Test]
    public void OutsideValueChange_ShowsItsMonth()
    {
        var cut = RenderBound([], Oct23);

        cut.Render(p => p.Add(x => x.Value, new DateOnly(2027, 2, 14)));

        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("February 2027"));
        Assert.That(Active(cut), Is.EqualTo("cal-d20270214"));
        Assert.That(cut.Find("#cal-d20270214").GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Disabled_NoPicks_NoTabStop()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, Oct23, p => p.Add(x => x.Disabled, true));

        await cut.Find("#cal-d20261014").ClickAsync(new MouseEventArgs());
        await KeyAsync(cut, "ArrowRight");

        Assert.That(raised, Is.Empty);
        Assert.That(Active(cut), Is.EqualTo("cal-d20261023"));
        Assert.That(cut.Find(".nx-calendar-grids").HasAttribute("tabindex"), Is.False);
        Assert.That(cut.Find(".nx-calendar-today-button").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void ShowTodayButtonOff_NoFooter()
    {
        var cut = RenderBound([], Oct23, p => p.Add(x => x.ShowTodayButton, false));

        Assert.That(cut.FindAll(".nx-calendar-today-button"), Is.Empty);
    }

    [Test]
    public void NotFocusable_HasNoTabStop()
    {
        var cut = RenderBound([], Oct23, p => p.Add(x => x.Focusable, false));

        Assert.That(cut.Find(".nx-calendar-grids").HasAttribute("tabindex"), Is.False);
        Assert.That(cut.Find(".nx-calendar-prev").GetAttribute("tabindex"), Is.EqualTo("-1"));
    }
}
