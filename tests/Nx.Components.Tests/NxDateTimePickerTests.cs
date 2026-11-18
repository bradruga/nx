using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxDateTimePickerTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTime March5 = new(2026, 3, 5, 14, 30, 0);
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-picker.js");
        module.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxDateTimePicker<T>> RenderBound<T>(List<T?> raised, T? initial = default,
        Action<ComponentParameterCollectionBuilder<NxDateTimePicker<T>>>? configure = null, string format = "M/d/yyyy h:mm tt")
    {
        IRenderedComponent<NxDateTimePicker<T>>? cut = null;
        cut = Render<NxDateTimePicker<T>>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.Culture, EnUs);
            p.Add(x => x.Id, "dt");
            p.Add(x => x.Format, format);
            p.Add(x => x.ValueChanged, (T? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static async Task TypeAndCommitAsync<T>(IRenderedComponent<NxDateTimePicker<T>> cut, string text)
    {
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
        await cut.Find("input").TriggerEventAsync("onchange", new ChangeEventArgs { Value = text });
    }

    private static Task KeyAsync<T>(IRenderedComponent<NxDateTimePicker<T>> cut, string key) =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });

    private static string? Text<T>(IRenderedComponent<NxDateTimePicker<T>> cut) => cut.Find("input").GetAttribute("value");

    // ── Rendering and format ──────────────────────────────────────────────────

    [Test]
    public void Render_ComboboxShowsFormat_PlaceholderIsThePattern()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.Label, "Starts"));

        var input = cut.Find(".nx-field.nx-datetimepicker input.nx-datetimepicker-input");
        Assert.That(input.GetAttribute("role"), Is.EqualTo("combobox"));
        Assert.That(input.GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
        Assert.That(Text(cut), Is.EqualTo("3/5/2026 2:30 PM"));
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("m/d/yyyy h:mm am/pm"));
        Assert.That(cut.Find("label").TextContent, Is.EqualTo("Starts"));
    }

    [Test]
    public void DefaultFormat_IsTheCulturesG()
    {
        var cut = RenderBound<DateTime?>([], March5, format: "g");
        Assert.That(Text(cut), Is.EqualTo("3/5/2026 2:30 PM"));
    }

    [Test]
    public void AllNumber24HourFormat_IsMasked_OthersAreNot()
    {
        var masked = RenderBound<DateTime?>([], March5, format: "MM/dd/yyyy HH:mm");
        Assert.That(Text(masked), Is.EqualTo("03/05/2026 14:30"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "mask" && (string?)i.Arguments[1] == "99/99/9999 99:99"), Is.True);
        Assert.That(masked.Find("input").GetAttribute("inputmode"), Is.EqualTo("numeric"));

        var free = RenderBound<DateTime?>([], March5);
        Assert.That(free.Find("input").HasAttribute("inputmode"), Is.False);
    }

    [Test]
    public void UnsupportedType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxDateTimePicker<DateOnly>>());
    }

    // ── Typing ────────────────────────────────────────────────────────────────

    [TestCase("3/6/2026 9:15 AM", 2026, 3, 6, 9, 15)]
    [TestCase("3/6/2026 21:15", 2026, 3, 6, 21, 15)]
    [TestCase("2026-03-06 09:15", 2026, 3, 6, 9, 15)]
    public async Task Typing_ParsesLeniently(string typed, int y, int mo, int d, int h, int mi)
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5);

        await TypeAndCommitAsync(cut, typed);

        Assert.That(raised, Is.EqualTo(new DateTime?[] { new DateTime(y, mo, d, h, mi, 0) }));
    }

    [Test]
    public async Task Typing_Unreadable_Reverts()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5);

        await TypeAndCommitAsync(cut, "next tuesday");

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("3/5/2026 2:30 PM"));
    }

    [Test]
    public async Task Typing_ClampedToMinAndMax()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5, p => p
            .Add(x => x.Min, new DateTime(2026, 3, 1, 8, 0, 0))
            .Add(x => x.Max, new DateTime(2026, 3, 31, 17, 0, 0)));

        await TypeAndCommitAsync(cut, "3/31/2026 9:00 PM");
        await TypeAndCommitAsync(cut, "2/1/2026 9:00 AM");

        Assert.That(raised, Is.EqualTo(new DateTime?[] { new DateTime(2026, 3, 31, 17, 0, 0), new DateTime(2026, 3, 1, 8, 0, 0) }));
    }

    [Test]
    public async Task Empty_IsNullForNullable_RevertsForDateTime()
    {
        var nullable = new List<DateTime?>();
        var a = RenderBound(nullable, March5);
        await TypeAndCommitAsync(a, "");
        Assert.That(nullable, Is.EqualTo(new DateTime?[] { null }));

        var plain = new List<DateTime>();
        var b = RenderBound(plain, March5);
        await TypeAndCommitAsync(b, "");
        Assert.That(plain, Is.Empty);
        Assert.That(Text(b), Is.EqualTo("3/5/2026 2:30 PM"));
    }

    [Test]
    public async Task Kind_IsKept()
    {
        var raised = new List<DateTime>();
        var cut = RenderBound(raised, DateTime.SpecifyKind(March5, DateTimeKind.Utc));

        await TypeAndCommitAsync(cut, "3/6/2026 9:00 AM");

        Assert.That(raised.Single().Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    // ── Popup ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Open_CalendarBesideTimeList_AtTheStep()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.TimeStep, 15));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-datetimepicker-popup .nx-calendar"), Is.Not.Null);
        var times = cut.FindAll(".nx-datetimepicker-time-list .nx-timepicker-item");
        Assert.That(times, Has.Count.EqualTo(96));
        Assert.That(cut.Find(".nx-timepicker-item-selected").TextContent.Trim(), Is.EqualTo("2:30 PM"));
        Assert.That(cut.Find(".nx-datetimepicker-times-title").TextContent, Is.EqualTo("Thu, Mar 5"));
    }

    [Test]
    public async Task PickDay_KeepsTime_ThenPickTime_Closes()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5);
        await KeyAsync(cut, "ArrowDown");

        await cut.Find("[data-date='2026-03-10']").ClickAsync(new MouseEventArgs());
        Assert.That(raised.Last(), Is.EqualTo(new DateTime(2026, 3, 10, 14, 30, 0)));
        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Has.Count.EqualTo(1));

        var nine = cut.FindAll(".nx-timepicker-item").Single(t => t.TextContent.Trim() == "9:00 AM");
        await nine.ClickAsync(new MouseEventArgs());

        Assert.That(raised.Last(), Is.EqualTo(new DateTime(2026, 3, 10, 9, 0, 0)));
        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task NoValue_PickTimeFirst_GoesOnToday()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound<DateTime?>(raised);
        await KeyAsync(cut, "ArrowDown");

        await cut.FindAll(".nx-timepicker-item").Single(t => t.TextContent.Trim() == "6:00 AM").ClickAsync(new MouseEventArgs());

        Assert.That(raised.Single(), Is.EqualTo(DateTime.Today.AddHours(6)));
    }

    [Test]
    public async Task Keyboard_EnterOnDay_MovesToTimes_ArrowsAndEnterPick()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5);
        await KeyAsync(cut, "ArrowDown");

        await KeyAsync(cut, "ArrowRight");           // Mar 6
        await KeyAsync(cut, "Enter");
        Assert.That(raised.Last(), Is.EqualTo(new DateTime(2026, 3, 6, 14, 30, 0)));
        Assert.That(cut.Find(".nx-timepicker-item-active").TextContent.Trim(), Is.EqualTo("2:30 PM"));
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Does.StartWith("dt-time-"));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(raised.Last(), Is.EqualTo(new DateTime(2026, 3, 6, 15, 30, 0)));
        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task Keyboard_ArrowLeftInTimes_BackToCalendar_EscapeCloses()
    {
        var cut = RenderBound<DateTime?>([], March5);
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");
        Assert.That(cut.FindAll(".nx-timepicker-item-active"), Has.Count.EqualTo(1));

        await KeyAsync(cut, "ArrowLeft");
        Assert.That(cut.FindAll(".nx-timepicker-item-active"), Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Does.Contain("calendar"));

        await KeyAsync(cut, "Escape");
        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task MinMax_LimitTimesOnTheirDays()
    {
        var cut = RenderBound<DateTime?>([], new DateTime(2026, 3, 5, 10, 0, 0), p => p
            .Add(x => x.TimeStep, 60)
            .Add(x => x.Min, new DateTime(2026, 3, 5, 8, 0, 0))
            .Add(x => x.Max, new DateTime(2026, 3, 9, 12, 0, 0)));
        await KeyAsync(cut, "ArrowDown");

        var times = cut.FindAll(".nx-timepicker-item").Select(t => t.TextContent.Trim()).ToList();
        Assert.That(times.First(), Is.EqualTo("8:00 AM"));
        Assert.That(times.Last(), Is.EqualTo("11:00 PM"));
    }

    [Test]
    public async Task Clearable_ButtonAndEscape()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5, p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-field-clear").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new DateTime?[] { null }));

        var other = new List<DateTime?>();
        var esc = RenderBound(other, March5, p => p.Add(x => x.Clearable, true));
        await KeyAsync(esc, "Escape");
        Assert.That(other, Is.EqualTo(new DateTime?[] { null }));
    }

    [Test]
    public void ReadOnlyAndDisabled_DoNotOpen()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.ReadOnly, true));
        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.True);
        Assert.That(cut.Find(".nx-datetimepicker-toggle").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void Disabled_InputAndToggleOff()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.Disabled, true));

        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-datetimepicker-toggle").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task ReadOnly_ArrowDownDoesNotOpen()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.ReadOnly, true));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task Typing_DisabledDay_Reverts()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5, p => p.Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Sunday));

        await TypeAndCommitAsync(cut, "3/8/2026 9:00 AM");     // a Sunday

        Assert.That(raised, Is.Empty);
        Assert.That(Text(cut), Is.EqualTo("3/5/2026 2:30 PM"));
    }

    [Test]
    public async Task Popup_DisabledDaysNotPickable()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5, p => p.Add(x => x.IsDateDisabled, d => d.DayOfWeek == DayOfWeek.Sunday));
        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find("[data-date='2026-03-08']").GetAttribute("aria-disabled"), Is.EqualTo("true"));
        await cut.Find("[data-date='2026-03-08']").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task PickDay_WithNoValue_IsMidnight()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound<DateTime?>(raised);
        await KeyAsync(cut, "ArrowDown");

        var first = cut.Find(".nx-calendar td[data-date]:not(.nx-calendar-other)").GetAttribute("data-date")!;
        await cut.Find($"[data-date='{first}']").ClickAsync(new MouseEventArgs());

        Assert.That(raised.Single(), Is.EqualTo(DateTime.ParseExact(first, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    [Test]
    public async Task PickDay_BeforeMinTimeOnMinDay_MovesToMin()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, new DateTime(2026, 3, 6, 7, 0, 0), p => p.Add(x => x.Min, new DateTime(2026, 3, 5, 8, 0, 0)));
        await KeyAsync(cut, "ArrowDown");

        await cut.Find("[data-date='2026-03-05']").ClickAsync(new MouseEventArgs());

        Assert.That(raised.Single(), Is.EqualTo(new DateTime(2026, 3, 5, 8, 0, 0)));
    }

    [Test]
    public async Task TypingWhileOpen_MovesCalendar()
    {
        var cut = RenderBound<DateTime?>([], March5);
        await KeyAsync(cut, "ArrowDown");

        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "7/4/2027 9:10 AM" });

        Assert.That(cut.Find(".nx-calendar-title").TextContent.Trim(), Is.EqualTo("July 2027"));
        Assert.That(cut.Find("input").GetAttribute("aria-activedescendant"), Is.EqualTo("dt-calendar-d20270704"));
    }

    [Test]
    public async Task TimeList_PageDownMovesSix_AltUpCloses()
    {
        var cut = RenderBound<DateTime?>([], March5);
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");                 // day picked: keys go to the times, 2:30 PM active

        await KeyAsync(cut, "PageDown");
        Assert.That(cut.Find(".nx-timepicker-item-active").TextContent.Trim(), Is.EqualTo("5:30 PM"));

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });
        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Is.Empty);
    }

    [Test]
    public async Task Blur_ClosesAndCommitsTypedText_RaisesFocusAndBlur()
    {
        var raised = new List<DateTime?>();
        var events = new List<string>();
        var cut = RenderBound(raised, March5, p => p
            .Add(x => x.OnFocus, (FocusEventArgs _) => events.Add("focus"))
            .Add(x => x.OnBlur, (FocusEventArgs _) => events.Add($"blur {raised.Count}")));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await KeyAsync(cut, "ArrowDown");
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "3/9/2026 8:00 AM" });
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        Assert.That(raised, Is.EqualTo(new DateTime?[] { new DateTime(2026, 3, 9, 8, 0, 0) }));
        Assert.That(cut.FindAll(".nx-datetimepicker-popup"), Is.Empty);
        Assert.That(events, Is.EqualTo(new[] { "focus", "blur 1" }));
    }

    [Test]
    public async Task OwnRender_KeepsTypedText_OutsideChangeReplacesIt()
    {
        var cut = RenderBound<DateTime?>([], March5);
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "3/9/20" });

        cut.Render(p => p.Add(x => x.Value, March5));
        Assert.That(Text(cut), Is.EqualTo("3/9/20"));

        cut.Render(p => p.Add(x => x.Value, new DateTime(2026, 4, 1, 9, 0, 0)));
        Assert.That(Text(cut), Is.EqualTo("4/1/2026 9:00 AM"));
    }

    [Test]
    public async Task ShowTodayButtonOff_AndWeekNumbers()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.ShowTodayButton, false).Add(x => x.ShowWeekNumbers, true));
        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll(".nx-calendar-today-button"), Is.Empty);
        Assert.That(cut.FindAll(".nx-datetimepicker-popup th.nx-calendar-week"), Is.Not.Empty);
    }

    [Test]
    public void ExtraAttributes_GoToTheInput()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.AddUnmatched("name", "starts"));

        Assert.That(cut.Find("input").GetAttribute("name"), Is.EqualTo("starts"));
    }

    // ── Phone sheet ───────────────────────────────────────────────────────────

    [Test]
    public async Task Sheet_Clear_EmptiesAndCloses()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5, p => p.Add(x => x.MobileMode, NxMobileMode.Always).Add(x => x.Clearable, true));

        await cut.Find(".nx-datetimepicker-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-datetimepicker-sheet-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new DateTime?[] { null }));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
    }

    [Test]
    public async Task Sheet_TimeTab_ShowsTheList()
    {
        var cut = RenderBound<DateTime?>([], March5, p => p.Add(x => x.MobileMode, NxMobileMode.Always).Add(x => x.TimeStep, 60));

        await cut.Find(".nx-datetimepicker-toggle").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".nx-datetimepicker-sheet-tab")[1].ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-datetimepicker-sheet-list button.nx-timepicker-item"), Has.Count.EqualTo(24));
        Assert.That(cut.FindAll(".nx-sheet .nx-calendar"), Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "lockScroll" && (bool)i.Arguments[0]!), Is.True);
    }

    [Test]
    public async Task Sheet_DateAndTimeTabs()
    {
        var raised = new List<DateTime?>();
        var cut = RenderBound(raised, March5, p => p.Add(x => x.MobileMode, NxMobileMode.Always).Add(x => x.Label, "Gate opens"));

        await cut.Find(".nx-datetimepicker-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find(".nx-sheet").GetAttribute("aria-label"), Is.EqualTo("Gate opens"));
        var tabs = cut.FindAll(".nx-datetimepicker-sheet-tab");
        Assert.That(tabs.Select(t => t.GetAttribute("aria-selected")), Is.EqualTo(new[] { "true", "false" }));
        Assert.That(tabs[0].TextContent, Does.Contain("3/5/2026"));
        Assert.That(tabs[1].TextContent, Does.Contain("2:30 PM"));

        await cut.Find("[data-date='2026-03-07']").ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".nx-datetimepicker-sheet-tab")[1].GetAttribute("aria-selected"), Is.EqualTo("true"));

        await cut.FindAll(".nx-datetimepicker-sheet-list .nx-timepicker-item").Single(t => t.TextContent.Trim() == "7:00 AM").ClickAsync(new MouseEventArgs());
        Assert.That(raised.Last(), Is.EqualTo(new DateTime(2026, 3, 7, 7, 0, 0)));
        Assert.That(cut.FindAll(".nx-sheet"), Is.Empty);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private sealed class Meeting
    {
        public DateTime? Start { get; set; }
    }

    [Test]
    public async Task Validation_AriaInvalid_AndNotifiesOnCommit()
    {
        var meeting = new Meeting();
        var context = new EditContext(meeting);
        var store = new ValidationMessageStore(context);
        var changed = new List<string>();
        context.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier.FieldName);
        var cut = Render<NxDateTimePicker<DateTime?>>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Culture, EnUs)
            .Add(x => x.Value, meeting.Start)
            .Add(x => x.ValueChanged, v => meeting.Start = v)
            .Add(x => x.ValueExpression, () => meeting.Start));

        store.Add(context.Field(nameof(Meeting.Start)), "Required");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));

        await TypeAndCommitAsync(cut, "3/5/2026 2:30 PM");
        Assert.That(changed, Is.EqualTo(new[] { "Start" }));
    }
}
