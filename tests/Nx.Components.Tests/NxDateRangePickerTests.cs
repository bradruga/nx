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
}
