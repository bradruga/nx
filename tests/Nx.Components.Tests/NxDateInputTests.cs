using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxDateInputTests : BunitContext
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-picker.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxDateInput<DateOnly?>> RenderBound(List<DateOnly?> raised, DateOnly? initial = null,
        Action<ComponentParameterCollectionBuilder<NxDateInput<DateOnly?>>>? configure = null, CultureInfo? culture = null)
    {
        IRenderedComponent<NxDateInput<DateOnly?>>? cut = null;
        cut = Render<NxDateInput<DateOnly?>>(p =>
        {
            p.Add(x => x.Id, "d");
            p.Add(x => x.Culture, culture ?? Us);
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (DateOnly? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static async Task TypeAsync(IRenderedComponent<NxDateInput<DateOnly?>> cut, string text)
    {
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = text });
    }

    [Test]
    public void Render_FieldWithoutCalendar_MaskAndStepperApplied()
    {
        var cut = RenderBound([], new DateOnly(2026, 10, 23), p => p.Add(x => x.Label, "Ship date"));

        Assert.That(cut.Find(".nx-field.nx-dateinput label[for=d]").TextContent, Is.EqualTo("Ship date"));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("10/23/2026"));
        Assert.That(cut.Find("input").GetAttribute("placeholder"), Is.EqualTo("mm/dd/yyyy"));
        Assert.That(cut.Find("input").GetAttribute("inputmode"), Is.EqualTo("numeric"));
        Assert.That(cut.FindAll("button"), Is.Empty);
        Assert.That(module.Invocations.Single(i => i.Identifier == "mask").Arguments[1], Is.EqualTo("99/99/9999"));
        var step = module.Invocations.Single(i => i.Identifier == "stepDate");
        Assert.That(step.Arguments[1], Is.EqualTo("99/99/9999"));
        Assert.That(step.Arguments[2], Is.EqualTo("Mdy"));
    }

    [Test]
    public void Order_FollowsTheCulture()
    {
        RenderBound([], culture: CultureInfo.GetCultureInfo("de-DE"));
        var step = module.Invocations.Single(i => i.Identifier == "stepDate");
        Assert.That(step.Arguments[1], Is.EqualTo("99.99.9999"));
        Assert.That(step.Arguments[2], Is.EqualTo("dMy"));
    }

    [Test]
    public async Task Typed_CommitsOnChange_ClampedToLimits()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.Max, new DateOnly(2026, 12, 31)));

        await TypeAsync(cut, "11/05/2026");
        await TypeAsync(cut, "03/01/2027");

        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 31) }));
    }

    [Test]
    public async Task Unreadable_Reverts_EnterCommits()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, new DateOnly(2026, 1, 2));

        await TypeAsync(cut, "13/45/x");
        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("1/2/2026"));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "02/03/2026" });
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(raised, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 2, 3) }));
    }

    [Test]
    public async Task Format_ShowsWhenBlurred_EditsWhenFocused()
    {
        var cut = RenderBound([], new DateOnly(2026, 3, 14), p => p.Add(x => x.Format, "MMM d, yyyy"));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Mar 14, 2026"));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("03/14/2026"));
        await cut.Find("input").BlurAsync(new FocusEventArgs());
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Mar 14, 2026"));
    }

    [Test]
    public async Task Clear_ButtonAndEscape()
    {
        var raised = new List<DateOnly?>();
        var cut = RenderBound(raised, new DateOnly(2026, 3, 14), p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-field-clear").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new DateOnly?[] { null }));

        var other = new List<DateOnly?>();
        var esc = RenderBound(other, new DateOnly(2026, 3, 14), p => p.Add(x => x.Clearable, true));
        await esc.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(other, Is.EqualTo(new DateOnly?[] { null }));
    }

    [Test]
    public async Task DateTime_KeepsItsTimeOfDay()
    {
        DateTime? raised = null;
        var cut = Render<NxDateInput<DateTime>>(p => p
            .Add(x => x.Culture, Us)
            .Add(x => x.Value, new DateTime(2026, 1, 2, 7, 30, 0))
            .Add(x => x.ValueChanged, v => raised = v));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "01/09/2026" });
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "01/09/2026" });

        Assert.That(raised, Is.EqualTo(new DateTime(2026, 1, 9, 7, 30, 0)));
    }

    [Test]
    public void UnsupportedType_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Render<NxDateInput<string>>());
}
