using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxChipListTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>A removable list that takes removed items out of its data, as an app would.</summary>
    private IRenderedComponent<NxChipList<string>> RenderRemovable(List<string> data, List<string> removed,
        Action<ComponentParameterCollectionBuilder<NxChipList<string>>>? configure = null)
    {
        IRenderedComponent<NxChipList<string>>? cut = null;
        cut = Render<NxChipList<string>>(p =>
        {
            p.Add(x => x.Data, data);
            p.Add(x => x.Removable, true);
            p.Add(x => x.AriaLabel, "Tickets");
            p.Add(x => x.OnRemove, (NxChipRemoveEventArgs<string> e) =>
            {
                removed.Add(e.Item);
                data.Remove(e.Item);
                cut?.Render(q => q.Add(x => x.Data, data.ToList()));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    [Test]
    public void Render_ListOfChips_OneTabStop_Roving()
    {
        var cut = Render<NxChipList<string>>(p => p.Add(x => x.Data, ["A", "B", "C"]).Add(x => x.AriaLabel, "Mixes"));

        var list = cut.Find("ul[role=list]");
        Assert.That(list.GetAttribute("aria-label"), Is.EqualTo("Mixes"));
        var chips = cut.FindAll(".nx-chiplist-chip");
        Assert.That(chips.Select(c => c.TextContent.Trim()), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(chips.Select(c => c.GetAttribute("tabindex")), Is.EqualTo(new[] { "0", "-1", "-1" }));
        Assert.That(cut.FindAll(".nx-chiplist-remove"), Is.Empty);
        Assert.That(module.Invocations.Single(i => i.Identifier == "roving").Arguments[1], Is.EqualTo(".nx-chiplist-chip"));
    }

    [Test]
    public void ItemText_AndTemplate()
    {
        var text = Render<NxChipList<int>>(p => p.Add(x => x.Data, [40212]).Add(x => x.ItemText, n => $"1-{n}"));
        Assert.That(text.Find(".nx-chiplist-text").TextContent, Is.EqualTo("1-40212"));

        var template = Render<NxChipList<int>>(p => p.Add(x => x.Data, [7])
            .Add(x => x.ItemTemplate, n => b => b.AddMarkupContent(0, $"<b>#{n}</b>")));
        Assert.That(template.Find(".nx-chiplist-content b").TextContent, Is.EqualTo("#7"));
    }

    [Test]
    public async Task RemoveButton_RaisesOnRemove_NamedForTheChip()
    {
        var data = new List<string> { "A", "B" };
        var removed = new List<string>();
        var cut = RenderRemovable(data, removed);

        var button = cut.FindAll(".nx-chiplist-remove")[1];
        Assert.That(button.GetAttribute("aria-label"), Is.EqualTo("Remove B"));
        Assert.That(button.GetAttribute("tabindex"), Is.EqualTo("-1"));
        await button.ClickAsync(new MouseEventArgs());

        Assert.That(removed, Is.EqualTo(new[] { "B" }));
        Assert.That(cut.FindAll(".nx-chiplist-chip"), Has.Count.EqualTo(1));
    }

    [TestCase("Delete")]
    [TestCase("Backspace")]
    public async Task Keys_RemoveTheFocusedChip(string key)
    {
        var removed = new List<string>();
        var cut = RenderRemovable(["A", "B", "C"], removed);

        await cut.FindAll(".nx-chiplist-chip")[1].FocusAsync(new FocusEventArgs());
        await cut.FindAll(".nx-chiplist-chip")[1].KeyDownAsync(new KeyboardEventArgs { Key = key });

        Assert.That(removed, Is.EqualTo(new[] { "B" }));
        Assert.That(cut.FindAll(".nx-chiplist-chip")[1].GetAttribute("tabindex"), Is.EqualTo("0"), "focus stays at the same place");
    }

    [Test]
    public async Task Disabled_NoRemoving()
    {
        var removed = new List<string>();
        var cut = RenderRemovable(["A"], removed, p => p.Add(x => x.Disabled, true));

        Assert.That(cut.FindAll(".nx-chiplist-remove"), Is.Empty);
        await cut.Find(".nx-chiplist-chip").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });
        Assert.That(removed, Is.Empty);
        Assert.That(cut.Find(".nx-chiplist-chip").GetAttribute("aria-disabled"), Is.EqualTo("true"));
    }

    [Test]
    public void Empty_NoDataText_VariantAndSize()
    {
        var empty = Render<NxChipList<string>>(p => p.Add(x => x.Data, []).Add(x => x.NoDataText, "No tags"));
        Assert.That(empty.Find(".nx-chiplist-empty").TextContent, Is.EqualTo("No tags"));

        var styled = Render<NxChipList<string>>(p => p.Add(x => x.Data, ["A"]).Add(x => x.Variant, NxChipVariant.Outline).Add(x => x.Size, NxSize.Large));
        Assert.That(styled.Find("ul").ClassList, Does.Contain("nx-chiplist-outline").And.Contain("nx-chiplist-lg"));
    }

    [Test]
    public void DuplicateItems_Render()
    {
        var cut = Render<NxChipList<string>>(p => p.Add(x => x.Data, ["SCC", "SCC"]));
        Assert.That(cut.FindAll(".nx-chiplist-chip"), Has.Count.EqualTo(2));
    }
}
