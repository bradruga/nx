using Bunit;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxLoaderTests : BunitContext
{
    // ── NxLoader ──────────────────────────────────────────────────────────────

    [Test]
    public void Loader_DefaultSpinner_ProgressBarNamedLoading()
    {
        var cut = Render<NxLoader>();

        var root = cut.Find(".nx-loader");
        Assert.That(root.ClassList, Is.EquivalentTo(new[] { "nx-loader", "nx-loader-spinner", "nx-loader-md" }));
        Assert.That(root.GetAttribute("role"), Is.EqualTo("progressbar"));
        Assert.That(root.GetAttribute("aria-label"), Is.EqualTo("Loading"));
        Assert.That(cut.FindAll(".nx-loader-ring"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Loader_ChildContent_ReplacesTheAnimation_KeepsBoxAndName()
    {
        var cut = Render<NxLoader>(p => p
            .Add(x => x.Type, NxLoaderType.Pulsing)
            .Add(x => x.Size, NxSize.Large)
            .Add(x => x.AriaLabel, "Batching")
            .AddChildContent("<svg class=\"mine\"></svg>"));

        var root = cut.Find(".nx-loader");
        Assert.That(root.ClassList, Is.EquivalentTo(new[] { "nx-loader", "nx-loader-custom", "nx-loader-lg" }));
        Assert.That(root.GetAttribute("role"), Is.EqualTo("progressbar"));
        Assert.That(root.GetAttribute("aria-label"), Is.EqualTo("Batching"));
        Assert.That(cut.FindAll(".mine"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll(".nx-loader-dot, .nx-loader-ring"), Is.Empty);
    }

    [TestCase(NxLoaderType.Pulsing, "nx-loader-pulsing", 3)]
    public void Loader_DotTypes(NxLoaderType type, string css, int dots)
    {
        var cut = Render<NxLoader>(p => p.Add(x => x.Type, type));

        Assert.That(cut.Find(".nx-loader").ClassList, Does.Contain(css));
        Assert.That(cut.FindAll(".nx-loader-dot"), Has.Count.EqualTo(dots));
        Assert.That(cut.FindAll(".nx-loader-ring"), Is.Empty);
    }

    [TestCase(NxSize.Small, "nx-loader-sm")]
    [TestCase(NxSize.Large, "nx-loader-lg")]
    public void Loader_Size(NxSize size, string css)
    {
        var cut = Render<NxLoader>(p => p.Add(x => x.Size, size));
        Assert.That(cut.Find(".nx-loader").ClassList, Does.Contain(css));
    }

    [Test]
    public void Loader_LabelClassStyleAttributes()
    {
        var cut = Render<NxLoader>(p => p
            .Add(x => x.AriaLabel, "Saving")
            .Add(x => x.Class, "mine")
            .Add(x => x.Style, "--nx-loader-color: red")
            .AddUnmatched("id", "busy"));

        var root = cut.Find(".nx-loader");
        Assert.That(root.GetAttribute("aria-label"), Is.EqualTo("Saving"));
        Assert.That(root.ClassList, Does.Contain("mine"));
        Assert.That(root.GetAttribute("style"), Is.EqualTo("--nx-loader-color: red"));
        Assert.That(root.Id, Is.EqualTo("busy"));
    }

    // ── NxLoaderContainer ─────────────────────────────────────────────────────

    [Test]
    public void Container_Default_LargeSpinnerNoText_BusyStatus()
    {
        var cut = Render<NxLoaderContainer>();

        var overlay = cut.Find(".nx-loader-container");
        Assert.That(overlay.GetAttribute("aria-busy"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-loader-container-panel").GetAttribute("role"), Is.EqualTo("status"));
        Assert.That(cut.Find(".nx-loader").ClassList, Does.Contain("nx-loader-lg").And.Contain("nx-loader-spinner"));
        Assert.That(cut.FindAll(".nx-loader-container-text"), Is.Empty);
    }

    [Test]
    public void Container_NotVisible_RendersNothing_ThenAppears()
    {
        var cut = Render<NxLoaderContainer>(p => p.Add(x => x.Visible, false));
        Assert.That(cut.Markup.Trim(), Is.Empty);

        cut.Render(p => p.Add(x => x.Visible, true));
        Assert.That(cut.FindAll(".nx-loader-container"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Container_TextTypeAndSize()
    {
        var cut = Render<NxLoaderContainer>(p => p
            .Add(x => x.Text, "Loading orders…")
            .Add(x => x.LoaderType, NxLoaderType.Pulsing)
            .Add(x => x.LoaderSize, NxSize.Small));

        Assert.That(cut.Find(".nx-loader-container-text").TextContent, Is.EqualTo("Loading orders…"));
        Assert.That(cut.Find(".nx-loader").GetAttribute("aria-label"), Is.EqualTo("Loading orders…"));
        Assert.That(cut.Find(".nx-loader").ClassList, Does.Contain("nx-loader-pulsing").And.Contain("nx-loader-sm"));
    }

    [Test]
    public void Container_ChildContentReplacesLoaderAndText()
    {
        var cut = Render<NxLoaderContainer>(p => p
            .Add(x => x.Text, "ignored")
            .AddChildContent("<strong class=\"mine\">Scanning page 3</strong>"));

        Assert.That(cut.Find(".nx-loader-container-panel strong.mine").TextContent, Is.EqualTo("Scanning page 3"));
        Assert.That(cut.FindAll(".nx-loader"), Is.Empty);
        Assert.That(cut.FindAll(".nx-loader-container-text"), Is.Empty);
    }

    [Test]
    public void Container_ClassStyleAttributesOnOverlay()
    {
        var cut = Render<NxLoaderContainer>(p => p
            .Add(x => x.Class, "dark-overlay")
            .Add(x => x.Style, "--nx-loader-overlay-bg: #25282e")
            .AddUnmatched("id", "page-loader"));

        var overlay = cut.Find(".nx-loader-container");
        Assert.That(overlay.ClassList, Does.Contain("dark-overlay"));
        Assert.That(overlay.GetAttribute("style"), Is.EqualTo("--nx-loader-overlay-bg: #25282e"));
        Assert.That(overlay.Id, Is.EqualTo("page-loader"));
    }
}
