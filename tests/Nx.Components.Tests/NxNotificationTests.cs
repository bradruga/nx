using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxNotificationTests : BunitContext
{
    private static List<string> Texts(IRenderedComponent<NxNotification> cut) =>
        cut.FindAll(".nx-notification:not(.nx-notification-leaving) .nx-notification-text").Select(t => t.TextContent).ToList();

    [Test]
    public void Render_EmptyLiveRegion_BottomRightByDefault()
    {
        var cut = Render<NxNotification>();

        var stack = cut.Find(".nx-notification-stack");
        Assert.That(stack.GetAttribute("aria-live"), Is.EqualTo("polite"));
        Assert.That(stack.ClassList, Does.Contain("nx-notification-bottom").And.Contain("nx-notification-right"));
        Assert.That(cut.FindAll(".nx-notification"), Is.Empty);
    }

    [TestCase(NxNotificationVerticalPosition.Top, NxNotificationHorizontalPosition.Left, "nx-notification-top", "nx-notification-left")]
    [TestCase(NxNotificationVerticalPosition.Bottom, NxNotificationHorizontalPosition.Center, "nx-notification-bottom", "nx-notification-center")]
    public void Position_Classes(NxNotificationVerticalPosition v, NxNotificationHorizontalPosition h, string vClass, string hClass)
    {
        var cut = Render<NxNotification>(p => p.Add(x => x.VerticalPosition, v).Add(x => x.HorizontalPosition, h));
        Assert.That(cut.Find(".nx-notification-stack").ClassList, Does.Contain(vClass).And.Contain(hClass));
    }

    [Test]
    public void Show_RendersTextTypeIconAndClose()
    {
        var cut = Render<NxNotification>();

        cut.InvokeAsync(() => cut.Instance.Show(new NxNotificationModel { Text = "Saved", Type = NxNotificationType.Success }));

        var item = cut.Find(".nx-notification");
        Assert.That(item.ClassList, Does.Contain("nx-notification-success"));
        Assert.That(item.GetAttribute("role"), Is.EqualTo("status"));
        Assert.That(cut.Find(".nx-notification-text").TextContent, Is.EqualTo("Saved"));
        Assert.That(cut.FindAll(".nx-notification-icon"), Has.Count.EqualTo(1));
        Assert.That(cut.Find(".nx-notification-close").GetAttribute("aria-label"), Is.EqualTo("Close notification"));
    }

    [Test]
    public void Error_IsAnAlert_NoIconNoClose()
    {
        var cut = Render<NxNotification>();

        cut.InvokeAsync(() => cut.Instance.Show(new NxNotificationModel { Text = "Failed", Type = NxNotificationType.Error, ShowIcon = false, Closable = false }));

        Assert.That(cut.Find(".nx-notification").GetAttribute("role"), Is.EqualTo("alert"));
        Assert.That(cut.FindAll(".nx-notification-icon"), Is.Empty);
        Assert.That(cut.FindAll(".nx-notification-close"), Is.Empty);
    }

    [TestCase(NxNotificationType.Info, "nx-notification-info")]
    [TestCase(NxNotificationType.Warning, "nx-notification-warning")]
    [TestCase(NxNotificationType.Neutral, "nx-notification-neutral")]
    public void Type_Class(NxNotificationType type, string css)
    {
        var cut = Render<NxNotification>();
        cut.InvokeAsync(() => cut.Instance.Show("x", type));
        Assert.That(cut.Find(".nx-notification").ClassList, Does.Contain(css));
    }

    [Test]
    public void Close_Hides()
    {
        var cut = Render<NxNotification>();
        cut.InvokeAsync(() => cut.Instance.Show("One"));

        cut.Find(".nx-notification-close").Click(new MouseEventArgs());

        Assert.That(cut.Find(".nx-notification").ClassList, Does.Contain("nx-notification-leaving"));
        cut.WaitForState(() => cut.FindAll(".nx-notification").Count == 0);
        Assert.That(cut.Instance.Visible, Is.Empty);
    }

    [Test]
    public void CloseAfter_ClosesByItself_ZeroStays()
    {
        var cut = Render<NxNotification>();
        cut.InvokeAsync(() => cut.Instance.Show(new NxNotificationModel { Text = "Quick", CloseAfter = 50 }));
        cut.InvokeAsync(() => cut.Instance.Show(new NxNotificationModel { Text = "Sticky", CloseAfter = 0 }));

        cut.WaitForState(() => Texts(cut).SequenceEqual(["Sticky"]), TimeSpan.FromSeconds(3));
        Thread.Sleep(300);
        Assert.That(Texts(cut), Is.EqualTo(new[] { "Sticky" }));
    }

    [Test]
    public void Hover_HoldsItOpen_LeavingCountsAgain()
    {
        var cut = Render<NxNotification>();
        cut.InvokeAsync(() => cut.Instance.Show(new NxNotificationModel { Text = "Held", CloseAfter = 150 }));

        cut.Find(".nx-notification").MouseEnter();
        Thread.Sleep(400);
        Assert.That(Texts(cut), Is.EqualTo(new[] { "Held" }));

        cut.Find(".nx-notification").MouseLeave();
        cut.WaitForState(() => Texts(cut).Count == 0, TimeSpan.FromSeconds(3));
    }

    [Test]
    public void MaxVisible_ClosesTheOldest()
    {
        var cut = Render<NxNotification>(p => p.Add(x => x.MaxVisible, 2));

        cut.InvokeAsync(() =>
        {
            cut.Instance.Show(new NxNotificationModel { Text = "1", CloseAfter = 0 });
            cut.Instance.Show(new NxNotificationModel { Text = "2", CloseAfter = 0 });
            cut.Instance.Show(new NxNotificationModel { Text = "3", CloseAfter = 0 });
        });

        Assert.That(Texts(cut), Is.EqualTo(new[] { "2", "3" }));
        Assert.That(cut.Instance.Visible.Select(m => m.Text), Is.EqualTo(new[] { "2", "3" }));
    }

    [Test]
    public void HideModel_AndHideAll()
    {
        var cut = Render<NxNotification>();
        NxNotificationModel? first = null;
        cut.InvokeAsync(() =>
        {
            first = cut.Instance.Show(new NxNotificationModel { Text = "A", CloseAfter = 0 });
            cut.Instance.Show(new NxNotificationModel { Text = "B", CloseAfter = 0 });
            cut.Instance.Show(new NxNotificationModel { Text = "C", CloseAfter = 0 });
        });

        cut.InvokeAsync(() => cut.Instance.Hide(first!));
        Assert.That(Texts(cut), Is.EqualTo(new[] { "B", "C" }));

        cut.InvokeAsync(() => cut.Instance.HideAll());
        cut.WaitForState(() => cut.FindAll(".nx-notification").Count == 0);
    }

    [Test]
    public void Show_FromAnotherThread()
    {
        var cut = Render<NxNotification>();
        Task.Run(() => cut.Instance.Show("From a timer")).Wait();
        cut.WaitForState(() => Texts(cut).SequenceEqual(["From a timer"]));
    }

    [Test]
    public void Defaults_OfTheModel()
    {
        var model = new NxNotificationModel();
        Assert.That(model.Type, Is.EqualTo(NxNotificationType.Info));
        Assert.That(model.Closable, Is.True);
        Assert.That(model.CloseAfter, Is.EqualTo(5000));
        Assert.That(model.ShowIcon, Is.True);
    }
}
