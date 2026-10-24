using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxCarouselTests : BunitContext
{
    private static readonly string[] Slides = ["One", "Two", "Three"];

    private IRenderedComponent<NxCarousel<string>> RenderBound(List<int> raised, int page = 1,
        Action<ComponentParameterCollectionBuilder<NxCarousel<string>>>? configure = null, bool auto = false)
    {
        IRenderedComponent<NxCarousel<string>>? cut = null;
        cut = Render<NxCarousel<string>>(p =>
        {
            p.Add(x => x.Data, Slides);
            p.Add(x => x.Page, page);
            p.Add(x => x.AutomaticPageChange, auto);
            p.Add(x => x.Template, s => b => b.AddMarkupContent(0, $"<span class=\"slide\">{s}</span>"));
            p.Add(x => x.PageChanged, (int v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Page, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static string Active(IRenderedComponent<NxCarousel<string>> cut) => cut.Find(".nx-carousel-slide-active").TextContent;

    [Test]
    public void Render_RegionWithSlides_OthersInert()
    {
        var cut = RenderBound([], 2, p => p.Add(x => x.AriaLabel, "Pictures"));

        var root = cut.Find("section.nx-carousel");
        Assert.That(root.GetAttribute("aria-roledescription"), Is.EqualTo("carousel"));
        Assert.That(root.GetAttribute("aria-label"), Is.EqualTo("Pictures"));
        var slides = cut.FindAll(".nx-carousel-slide");
        Assert.That(slides.Select(s => s.GetAttribute("aria-label")), Is.EqualTo(new[] { "1 of 3", "2 of 3", "3 of 3" }));
        Assert.That(slides[0].HasAttribute("inert"), Is.True);
        Assert.That(slides[1].HasAttribute("inert"), Is.False);
        Assert.That(Active(cut), Is.EqualTo("Two"));
        Assert.That(root.GetAttribute("style"), Does.Contain("--nx-carousel-index: 1"));
    }

    [Test]
    public async Task Arrows_Wrap_RaisePageChanged()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 3);

        await cut.Find(".nx-carousel-next").ClickAsync(new MouseEventArgs());
        Assert.That(Active(cut), Is.EqualTo("One"));
        Assert.That(cut.Find(".nx-carousel-track").ClassList, Does.Contain("nx-carousel-track-jump"));
        await cut.Find(".nx-carousel-prev").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { 1, 3 }));
    }

    [Test]
    public async Task Dots_GoToTheSlide_CurrentMarked()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised);

        await cut.FindAll(".nx-carousel-dot")[2].ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { 3 }));
        Assert.That(cut.FindAll(".nx-carousel-dot")[2].GetAttribute("aria-current"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-carousel-dot")[0].GetAttribute("aria-label"), Is.EqualTo("Slide 1"));
    }

    [Test]
    public async Task Keys_AndSwipe()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised);

        await cut.Find("section").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await cut.Find(".nx-carousel-viewport").PointerDownAsync(new PointerEventArgs { ClientX = 300 });
        await cut.Find(".nx-carousel-viewport").PointerUpAsync(new PointerEventArgs { ClientX = 200 });
        await cut.Find(".nx-carousel-viewport").PointerDownAsync(new PointerEventArgs { ClientX = 100 });
        await cut.Find(".nx-carousel-viewport").PointerUpAsync(new PointerEventArgs { ClientX = 120 });

        Assert.That(raised, Is.EqualTo(new[] { 2, 3 }), "a 20 px move is not a swipe");
    }

    [Test]
    public void NoArrowsNoDots_NoPause()
    {
        var cut = RenderBound([], auto: true, configure: p => p.Add(x => x.Arrows, false).Add(x => x.Pageable, false));
        Assert.That(cut.FindAll(".nx-carousel-arrow"), Is.Empty);
        Assert.That(cut.FindAll(".nx-carousel-pager"), Is.Empty);
    }

    [Test]
    public async Task PauseButton_StopsTheRotation()
    {
        var cut = RenderBound([], auto: true);
        Assert.That(cut.Instance.Running, Is.True);

        await cut.Find(".nx-carousel-pause").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Instance.Running, Is.False);
        Assert.That(cut.Find(".nx-carousel-pause").GetAttribute("aria-label"), Is.EqualTo("Start slide rotation"));
    }

    [Test]
    public async Task HoverAndFocus_Pause()
    {
        var cut = RenderBound([], auto: true);

        await cut.Find("section").MouseEnterAsync(new MouseEventArgs());
        Assert.That(cut.Instance.Running, Is.False);
        await cut.Find("section").MouseLeaveAsync(new MouseEventArgs());
        Assert.That(cut.Instance.Running, Is.True);
        await cut.Find("section").FocusInAsync(new FocusEventArgs());
        Assert.That(cut.Instance.Running, Is.False);
    }

    [Test]
    public void Rotation_MovesOnByItself()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, auto: true, configure: p => p.Add(x => x.AutomaticPageChangeInterval, 500));

        cut.WaitForState(() => raised.Count >= 2, TimeSpan.FromSeconds(5));

        Assert.That(raised.Take(2), Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public void NewInterval_CountsFromNow()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.AutomaticPageChangeInterval, 60_000), auto: true);

        cut.Render(p => p.Add(x => x.AutomaticPageChangeInterval, 500));

        cut.WaitForState(() => raised.Count >= 1, TimeSpan.FromSeconds(5));
        Assert.That(raised[0], Is.EqualTo(2));
    }

    [Test]
    public void OneItem_NoControls()
    {
        var cut = Render<NxCarousel<string>>(p => p.Add(x => x.Data, ["Only"]));
        Assert.That(cut.FindAll(".nx-carousel-arrow, .nx-carousel-pager"), Is.Empty);
        Assert.That(cut.Instance.Running, Is.False);
    }
}
