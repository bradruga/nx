using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxSchedulerTests : BunitContext
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTime Wednesday = new(2026, 11, 11);   // the week of Sun 11/8 – Sat 11/14

    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-scheduler.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private sealed record Item(string Title, DateTime Start, DateTime End);

    private IRenderedComponent<NxScheduler<Item>> RenderWeek(IEnumerable<Item> items,
        Action<ComponentParameterCollectionBuilder<NxScheduler<Item>>>? configure = null, CultureInfo? culture = null) =>
        Render<NxScheduler<Item>>(p =>
        {
            p.Add(x => x.Data, items);
            p.Add(x => x.ItemStart, i => i.Start);
            p.Add(x => x.ItemEnd, i => i.End);
            p.Add(x => x.ItemTitle, i => i.Title);
            p.Add(x => x.Date, Wednesday);
            p.Add(x => x.Culture, culture ?? Us);
            p.Add(x => x.SlotDuration, 60);
            p.Add(x => x.SlotDivisions, 1);
            configure?.Invoke(p);
        });

    private static Dictionary<string, double> StyleOf(AngleSharp.Dom.IElement e) =>
        e.GetAttribute("style")!.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(':'))
            .ToDictionary(kv => kv[0].Trim(), kv => double.Parse(kv[1].Trim().TrimEnd('%'), CultureInfo.InvariantCulture));

    [Test]
    public void Render_WeekFromSunday_RangeAndHours()
    {
        var cut = RenderWeek([]);

        Assert.That(cut.Find(".nx-scheduler-range").TextContent, Is.EqualTo("November 8 – 14, 2026"));
        var heads = cut.FindAll(".nx-scheduler-day-head");
        Assert.That(heads, Has.Count.EqualTo(7));
        Assert.That(heads[0].TextContent, Does.Contain("Sun").And.Contain("8"));
        Assert.That(cut.FindAll(".nx-scheduler-day")[3].GetAttribute("aria-label"), Is.EqualTo("Wednesday, November 11, 2026"));
        // 00:00 – 23:59 in one-hour rows: 24 rows a day.
        Assert.That(cut.FindAll(".nx-scheduler-day")[0].QuerySelectorAll(".nx-scheduler-slot"), Has.Length.EqualTo(24));
        Assert.That(cut.FindAll(".nx-scheduler-time")[7].TextContent, Is.EqualTo("7 AM"));
    }

    [Test]
    public void WorkHours_AreNotShaded()
    {
        var cut = RenderWeek([], p => p.Add(x => x.WorkDayStart, new TimeOnly(7, 0)).Add(x => x.WorkDayEnd, new TimeOnly(19, 0)));

        var slots = cut.FindAll(".nx-scheduler-day")[1].QuerySelectorAll(".nx-scheduler-slot");
        Assert.That(slots[6].ClassList, Does.Contain("nx-scheduler-slot-off"));
        Assert.That(slots[7].ClassList, Does.Not.Contain("nx-scheduler-slot-off"));
        Assert.That(slots[18].ClassList, Does.Not.Contain("nx-scheduler-slot-off"));
        Assert.That(slots[19].ClassList, Does.Contain("nx-scheduler-slot-off"));
    }

    [Test]
    public void Item_PlacedByItsTimes()
    {
        var cut = RenderWeek([new("Pour", Wednesday.AddHours(6), Wednesday.AddHours(12))]);

        var events = cut.FindAll(".nx-scheduler-day")[3].QuerySelectorAll(".nx-scheduler-event");
        Assert.That(events, Has.Length.EqualTo(1));
        var style = StyleOf(events[0]);
        Assert.That(style["top"], Is.EqualTo(25));
        Assert.That(style["height"], Is.EqualTo(25));
        Assert.That(style["width"], Is.EqualTo(100));
        Assert.That(events[0].QuerySelector(".nx-scheduler-event-title")!.TextContent, Is.EqualTo("Pour"));
        Assert.That(events[0].QuerySelector(".nx-scheduler-event-time")!.TextContent, Is.EqualTo("6:00 AM – 12:00 PM"));
        Assert.That(events[0].GetAttribute("role"), Is.EqualTo("listitem"));
        Assert.That(events[0].QuerySelector(".nx-visually-hidden")!.TextContent, Is.EqualTo("Pour, Wed 6:00 AM – 12:00 PM"));
    }

    [Test]
    public void Overnight_SplitsAtMidnight()
    {
        var cut = RenderWeek([new("Night", Wednesday.AddHours(23), Wednesday.AddDays(1).AddHours(7))]);

        var days = cut.FindAll(".nx-scheduler-day");
        var wed = StyleOf(days[3].QuerySelector(".nx-scheduler-event")!);
        var thu = StyleOf(days[4].QuerySelector(".nx-scheduler-event")!);
        Assert.That(wed["top"], Is.EqualTo(95.833).Within(0.01));
        Assert.That(wed["height"], Is.EqualTo(4.167).Within(0.01));
        Assert.That(thu["top"], Is.EqualTo(0));
        Assert.That(thu["height"], Is.EqualTo(29.167).Within(0.01));
        Assert.That(days[4].QuerySelector(".nx-scheduler-event-time")!.TextContent, Is.EqualTo("11:00 PM – Thu 7:00 AM"));
    }

    [Test]
    public void Overlaps_ShareTheWidth()
    {
        var monday = Wednesday.AddDays(-2);
        var cut = RenderWeek(
        [
            new("A", monday.AddHours(8), monday.AddHours(9)),
            new("B", monday.AddHours(8.5), monday.AddHours(10)),
            new("C", monday.AddHours(9), monday.AddHours(9.5)),
            new("D", monday.AddHours(11), monday.AddHours(12)),
        ]);

        var events = cut.FindAll(".nx-scheduler-day")[1].QuerySelectorAll(".nx-scheduler-event").Select(StyleOf).ToList();
        // A, B and C overlap: A and C share column 0 (C starts when A ends), B takes column 1.
        Assert.That(events[0]["width"], Is.EqualTo(50));
        Assert.That(events[0]["left"], Is.EqualTo(0));
        Assert.That(events[1]["left"], Is.EqualTo(50));
        Assert.That(events[2]["left"], Is.EqualTo(0));
        Assert.That(events[3]["width"], Is.EqualTo(100));
    }

    [Test]
    public void OutsideTheHours_Clipped_OtherWeeks_Skipped()
    {
        var cut = RenderWeek(
        [
            new("Early", Wednesday.AddHours(5), Wednesday.AddHours(8)),
            new("Next week", Wednesday.AddDays(7).AddHours(9), Wednesday.AddDays(7).AddHours(10)),
            new("Backwards", Wednesday.AddHours(9), Wednesday.AddHours(8)),
        ], p => p.Add(x => x.StartTime, new TimeOnly(6, 0)).Add(x => x.EndTime, new TimeOnly(18, 0)));

        var events = cut.FindAll(".nx-scheduler-event");
        Assert.That(events, Has.Count.EqualTo(1));
        var style = StyleOf(events[0]);
        Assert.That(style["top"], Is.EqualTo(0));
        Assert.That(style["height"], Is.EqualTo(100.0 * 2 / 12).Within(0.01));
    }

    [Test]
    public void Template_RendersTheItem()
    {
        var cut = RenderWeek([new("Pour", Wednesday.AddHours(6), Wednesday.AddHours(8))],
            p => p.Add(x => x.ItemTemplate, item => b => b.AddMarkupContent(0, $"<b class=\"mine\">{item.Title}!</b>")));
        Assert.That(cut.Find(".nx-scheduler-event .mine").TextContent, Is.EqualTo("Pour!"));
        Assert.That(cut.FindAll(".nx-scheduler-event-title"), Is.Empty);
    }

    [Test]
    public async Task Navigation_RaisesDateChanged()
    {
        var dates = new List<DateTime>();
        var cut = RenderWeek([], p => p.Add(x => x.DateChanged, (DateTime d) => dates.Add(d)));

        await cut.Find("[data-nx-scheduler=next]").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find(".nx-scheduler-range").TextContent, Is.EqualTo("November 15 – 21, 2026"));
        await cut.Find("[data-nx-scheduler=previous]").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-nx-scheduler=previous]").ClickAsync(new MouseEventArgs());

        Assert.That(dates, Is.EqualTo(new[] { Wednesday.AddDays(7), Wednesday, Wednesday.AddDays(-7) }));
        Assert.That(cut.Find(".nx-scheduler-range").TextContent, Is.EqualTo("Nov 1 – Nov 7, 2026").Or.EqualTo("November 1 – 7, 2026"));
    }

    [Test]
    public void Culture_MondayFirst_24Hours()
    {
        var cut = RenderWeek([], culture: CultureInfo.GetCultureInfo("en-GB"));
        Assert.That(cut.FindAll(".nx-scheduler-day-head")[0].TextContent, Does.Contain("Mon").And.Contain("9"));
        Assert.That(cut.FindAll(".nx-scheduler-time")[13].TextContent, Is.EqualTo("13:00"));
    }

    [Test]
    public void Opens_ScrolledToTheWorkDay()
    {
        RenderWeek([], p => p.Add(x => x.WorkDayStart, new TimeOnly(7, 0)));
        var scroll = module.Invocations.Single(i => i.Identifier == "scrollToRow");
        Assert.That(scroll.Arguments[1], Is.EqualTo(7.0));
    }

    [Test]
    public async Task Today_MovesToThisWeek_DisabledThere()
    {
        var dates = new List<DateTime>();
        var cut = Render<NxScheduler<Item>>(p => p
            .Add(x => x.ItemStart, i => i.Start)
            .Add(x => x.ItemEnd, i => i.End)
            .Add(x => x.Date, DateTime.Today.AddDays(-30))
            .Add(x => x.Culture, Us)
            .Add(x => x.DateChanged, (DateTime d) => dates.Add(d)));
        Assert.That(cut.Find("[data-nx-scheduler=today]").HasAttribute("disabled"), Is.False);
        Assert.That(cut.FindAll(".nx-scheduler-day.nx-scheduler-today"), Is.Empty);

        await cut.Find("[data-nx-scheduler=today]").ClickAsync(new MouseEventArgs());

        Assert.That(dates, Is.EqualTo(new[] { DateTime.Today }));
        Assert.That(cut.Find("[data-nx-scheduler=today]").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-scheduler-day.nx-scheduler-today").GetAttribute("aria-label"), Is.EqualTo(DateTime.Today.ToString("D", Us)));
    }

    [Test]
    public void Date_FromOutside_ShowsItsWeek()
    {
        var cut = RenderWeek([]);

        cut.Render(p => p.Add(x => x.Date, new DateTime(2026, 12, 2)));

        Assert.That(cut.Find(".nx-scheduler-range").TextContent, Is.EqualTo("Nov 29 – Dec 5, 2026"));
    }

    [Test]
    public void Range_AcrossAYear_NamesBothYears()
    {
        var cut = RenderWeek([]);

        cut.Render(p => p.Add(x => x.Date, new DateTime(2026, 12, 31)));

        Assert.That(cut.Find(".nx-scheduler-range").TextContent, Is.EqualTo("Dec 27, 2026 – Jan 2, 2027"));
    }

    [Test]
    public void SlotDivisions_SplitTheHour_LabelOnlyTheHour()
    {
        var cut = RenderWeek([]);

        cut.Render(p => p.Add(x => x.SlotDivisions, 2));

        var slots = cut.FindAll(".nx-scheduler-day")[0].QuerySelectorAll(".nx-scheduler-slot");
        Assert.That(slots, Has.Length.EqualTo(48));
        Assert.That(slots[2].ClassList, Does.Contain("nx-scheduler-slot-major"));
        Assert.That(slots[3].ClassList, Does.Not.Contain("nx-scheduler-slot-major"));
        var times = cut.FindAll(".nx-scheduler-time");
        Assert.That((times[2].TextContent, times[3].TextContent), Is.EqualTo(("1 AM", "")));
    }

    [Test]
    public void StartAndEndTime_LimitTheRows()
    {
        var cut = RenderWeek([], p => p.Add(x => x.StartTime, new TimeOnly(6, 0)).Add(x => x.EndTime, new TimeOnly(18, 30)));

        // 6:00 to 18:30 rounds up to whole one-hour rows: 13.
        Assert.That(cut.FindAll(".nx-scheduler-day")[0].QuerySelectorAll(".nx-scheduler-slot"), Has.Length.EqualTo(13));
        Assert.That(cut.FindAll(".nx-scheduler-time")[0].TextContent, Is.EqualTo("6 AM"));
        Assert.That(cut.Find(".nx-scheduler-grid").GetAttribute("style"), Is.EqualTo("--nx-scheduler-rows: 13"));
    }

    [Test]
    public void Opens_ScrolledToTheWorkDay_CountingFromStartTimeInRows()
    {
        // Half-hour rows (the default SlotDivisions of 2) from 6:00: 8:00 is the fifth row.
        Render<NxScheduler<Item>>(p => p
            .Add(x => x.ItemStart, i => i.Start)
            .Add(x => x.ItemEnd, i => i.End)
            .Add(x => x.Date, Wednesday)
            .Add(x => x.StartTime, new TimeOnly(6, 0))
            .Add(x => x.WorkDayStart, new TimeOnly(8, 0)));

        Assert.That(module.Invocations.Single(i => i.Identifier == "scrollToRow").Arguments[1], Is.EqualTo(4.0));
    }

    private sealed class Shift
    {
        public string Name { get; init; } = "";
        public DateTimeOffset From { get; init; }
        public DateTimeOffset To { get; init; }
    }

    [Test]
    public void Lambdas_ReadAnyProperty_DateTimeOffsetTimes()
    {
        var shift = new Shift { Name = "Days", From = new DateTimeOffset(Wednesday.AddHours(6)), To = new DateTimeOffset(Wednesday.AddHours(12)) };
        var cut = Render<NxScheduler<Shift>>(p => p
            .Add(x => x.Data, new[] { shift })
            .Add(x => x.Date, Wednesday)
            .Add(x => x.Culture, Us)
            .Add(x => x.ItemStart, s => s.From.LocalDateTime)
            .Add(x => x.ItemEnd, s => s.To.LocalDateTime)
            .Add(x => x.ItemTitle, s => s.Name));

        var placed = cut.FindAll(".nx-scheduler-day")[3].QuerySelector(".nx-scheduler-event")!;
        Assert.That(placed.QuerySelector(".nx-scheduler-event-title")!.TextContent, Is.EqualTo("Days"));
        Assert.That(StyleOf(placed)["top"], Is.EqualTo(25));
    }

    private sealed record Untitled(DateTime Start, DateTime End);

    [Test]
    public void NoItemTitle_ShowsTheTimeOnly()
    {
        var cut = Render<NxScheduler<Untitled>>(p => p
            .Add(x => x.Data, new[] { new Untitled(Wednesday.AddHours(6), Wednesday.AddHours(8)) })
            .Add(x => x.ItemStart, u => u.Start)
            .Add(x => x.ItemEnd, u => u.End)
            .Add(x => x.Date, Wednesday)
            .Add(x => x.Culture, Us));

        Assert.That(cut.Find(".nx-scheduler-event .nx-visually-hidden").TextContent, Is.EqualTo("Wed 6:00 AM – 8:00 AM"));
    }

    [Test]
    public void AriaLabel_NamesTheGrid_DefaultTheRange()
    {
        Assert.That(RenderWeek([]).Find(".nx-scheduler-grid").GetAttribute("aria-label"), Is.EqualTo("November 8 – 14, 2026"));
        Assert.That(RenderWeek([], p => p.Add(x => x.AriaLabel, "Shifts")).Find(".nx-scheduler-grid").GetAttribute("aria-label"), Is.EqualTo("Shifts"));
    }

    [Test]
    public void MissingItemStart_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxScheduler<Item>>(p => p
            .Add(x => x.Data, [new("A", Wednesday, Wednesday.AddHours(1))])
            .Add(x => x.ItemEnd, i => i.End)));
    }
}
