using Bunit;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxMediaQueryTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-mediaquery.js");
        module.Setup<NxMediaQuery.WatchResult>("watch", _ => true).SetResult(new NxMediaQuery.WatchResult(7, true));
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
    }

    [Test]
    public void RendersNothing_ReportsTheFirstAnswer()
    {
        var reported = new List<bool>();
        var cut = Render<NxMediaQuery>(p => p.Add(x => x.Media, "(min-width: 768px)").Add(x => x.MatchesChanged, v => reported.Add(v)));

        Assert.That(cut.Markup.Trim(), Is.Empty);
        cut.WaitForState(() => reported.Count == 1);
        Assert.That(reported, Is.EqualTo(new[] { true }));
        Assert.That(module.Invocations.Single(i => i.Identifier == "watch").Arguments[0], Is.EqualTo("(min-width: 768px)"));
    }

    [Test]
    public async Task Changes_AreReported_RepeatsAreNot()
    {
        var reported = new List<bool>();
        var cut = Render<NxMediaQuery>(p => p.Add(x => x.Media, "(pointer: coarse)").Add(x => x.MatchesChanged, v => reported.Add(v)));
        cut.WaitForState(() => reported.Count == 1);

        await cut.Instance.OnMediaChanged(false);
        await cut.Instance.OnMediaChanged(false);
        await cut.Instance.OnMediaChanged(true);

        Assert.That(reported, Is.EqualTo(new[] { true, false, true }));
    }

    [Test]
    public void NewMedia_UnwatchesTheOld_ReportsAgain()
    {
        var reported = new List<bool>();
        var cut = Render<NxMediaQuery>(p => p.Add(x => x.Media, "(min-width: 768px)").Add(x => x.MatchesChanged, v => reported.Add(v)));
        cut.WaitForState(() => reported.Count == 1);

        cut.Render(p => p.Add(x => x.Media, "(min-width: 992px)"));

        cut.WaitForState(() => reported.Count == 2);
        Assert.That(module.Invocations.Count(i => i.Identifier == "watch"), Is.EqualTo(2));
        Assert.That(module.Invocations.Single(i => i.Identifier == "unwatch").Arguments[0], Is.EqualTo(7));
    }

    [Test]
    public async Task Dispose_Unwatches()
    {
        var reported = new List<bool>();
        var cut = Render<NxMediaQuery>(p => p.Add(x => x.Media, "(min-width: 768px)").Add(x => x.MatchesChanged, v => reported.Add(v)));
        cut.WaitForState(() => reported.Count == 1);

        await DisposeComponentsAsync();

        Assert.That(module.Invocations.Any(i => i.Identifier == "unwatch"), Is.True);
    }
}
