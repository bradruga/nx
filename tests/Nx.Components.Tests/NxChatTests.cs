using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxChatTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-chat.js");
        module.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    private sealed class Msg
    {
        public int Id { get; init; }
        public string Author { get; init; } = "";
        public string? Name { get; init; }
        public string? Picture { get; init; }
        public string? Text { get; init; }
        public DateTime Sent { get; init; }
        public bool Typing { get; init; }
    }

    private static readonly DateTime Today = DateTime.Today;

    private static List<Msg> Conversation() =>
    [
        new() { Id = 1, Author = "dana", Name = "Dana Ortiz", Text = "Stripped.", Sent = Today.AddDays(-1).AddHours(16) },
        new() { Id = 2, Author = "me", Text = "Thanks!", Sent = Today.AddDays(-1).AddHours(16).AddMinutes(2) },
        new() { Id = 3, Author = "sam", Name = "Sam Lee", Text = "Trucks at 6:30.", Sent = Today.AddHours(7) },
        new() { Id = 4, Author = "sam", Name = "Sam Lee", Text = "Three loads\nthen every 40 min.", Sent = Today.AddHours(7).AddMinutes(1) },
    ];

    private IRenderedComponent<NxChat<Msg>> RenderChat(List<Msg> data, Action<ComponentParameterCollectionBuilder<NxChat<Msg>>>? configure = null) =>
        Render<NxChat<Msg>>(p =>
        {
            p.Add(x => x.Id, "chat");
            p.Add(x => x.Data, data);
            p.Add(x => x.AuthorId, "me");
            p.Add(x => x.IdField, nameof(Msg.Id));
            p.Add(x => x.AuthorIdField, nameof(Msg.Author));
            p.Add(x => x.AuthorNameField, nameof(Msg.Name));
            p.Add(x => x.AuthorImageUrlField, nameof(Msg.Picture));
            p.Add(x => x.TextField, nameof(Msg.Text));
            p.Add(x => x.TimestampField, nameof(Msg.Sent));
            p.Add(x => x.IsTypingField, nameof(Msg.Typing));
            p.Add(x => x.Culture, CultureInfo.GetCultureInfo("en-US"));
            configure?.Invoke(p);
        });

    [Test]
    public void Render_GroupsByAuthorAndDay()
    {
        var cut = RenderChat(Conversation());

        var groups = cut.FindAll(".nx-chat-group");
        Assert.That(groups, Has.Count.EqualTo(3));
        Assert.That(groups[1].ClassList, Does.Contain("nx-chat-group-own"));
        Assert.That(groups[2].QuerySelectorAll(".nx-chat-message"), Has.Length.EqualTo(2));
        Assert.That(cut.FindAll(".nx-chat-author").Select(a => a.TextContent), Is.EqualTo(new[] { "Dana Ortiz", "Sam Lee" }));
        Assert.That(cut.FindAll(".nx-chat-day").Select(d => d.TextContent), Is.EqualTo(new[] { "Yesterday", "Today" }));
        Assert.That(groups[2].QuerySelector(".nx-chat-time")!.TextContent, Is.EqualTo("7:01 AM"));
        Assert.That(groups[2].QuerySelectorAll(".nx-chat-text")[1].TextContent, Is.EqualTo("Three loads\nthen every 40 min."));
    }

    [Test]
    public void Avatar_PictureElseInitialsElseIcon()
    {
        var cut = RenderChat(
        [
            new() { Id = 1, Author = "a", Name = "Priya Nair", Picture = "/p.png", Text = "1" },
            new() { Id = 2, Author = "b", Name = "Sam Lee", Text = "2" },
            new() { Id = 3, Author = "c", Text = "3" },
        ]);

        var avatars = cut.FindAll(".nx-chat-avatar");
        Assert.That(avatars[0].QuerySelector("img")!.GetAttribute("src"), Is.EqualTo("/p.png"));
        Assert.That(avatars[1].TextContent.Trim(), Is.EqualTo("SL"));
        Assert.That(avatars[2].QuerySelector("svg"), Is.Not.Null);
        Assert.That(avatars.All(a => a.GetAttribute("aria-hidden") == "true"), Is.True);
    }

    [Test]
    public void Accessibility_LogAndNamedBox()
    {
        var cut = RenderChat(Conversation(), p => p.Add(x => x.AriaLabel, "Crew"));

        var log = cut.Find(".nx-chat-messages");
        Assert.That(log.GetAttribute("role"), Is.EqualTo("log"));
        Assert.That(log.GetAttribute("aria-live"), Is.EqualTo("polite"));
        Assert.That(log.GetAttribute("aria-label"), Is.EqualTo("Crew"));
        Assert.That(cut.Find("#chat-input").GetAttribute("aria-label"), Is.EqualTo("Message to Crew"));
        Assert.That(cut.Find(".nx-chat-send").GetAttribute("aria-label"), Is.EqualTo("Send"));
        Assert.That(cut.Find(".nx-chat-group-own .nx-visually-hidden").TextContent, Is.EqualTo("You"));
    }

    [Test]
    public void Typing_ShowsDots()
    {
        var cut = RenderChat([new() { Id = 1, Author = "bot", Typing = true }]);

        var dots = cut.Find(".nx-chat-typing");
        Assert.That(dots.GetAttribute("role"), Is.EqualTo("status"));
        Assert.That(dots.QuerySelectorAll("i"), Has.Length.EqualTo(3));
    }

    [Test]
    public void Template_GetsTheMessage()
    {
        var cut = RenderChat(Conversation(), p => p.Add(x => x.MessageContentTemplate,
            context => b => b.AddContent(0, $"[{context.Message.Id}:{(context.IsOwn ? "own" : "other")}]")));

        Assert.That(cut.FindAll(".nx-chat-bubble").Select(b => b.TextContent),
            Is.EqualTo(new[] { "[1:other]", "[2:own]", "[3:other]", "[4:other]" }));
    }

    [Test]
    public void Empty_ShowsNoDataText()
    {
        var cut = RenderChat([], p => p.Add(x => x.NoDataText, "Say hello"));
        Assert.That(cut.Find(".nx-chat-empty").TextContent, Is.EqualTo("Say hello"));
    }

    [Test]
    public void WidthAndSize_OnTheOuterElement()
    {
        var cut = RenderChat([], p => p
            .Add(x => x.MessageWidthMode, NxChatMessageWidthMode.Full)
            .Add(x => x.Height, "100%")
            .Add(x => x.Width, "480px"));

        var outer = cut.Find("#chat");
        Assert.That(outer.ClassList, Does.Contain("nx-chat-full"));
        Assert.That(outer.GetAttribute("style"), Is.EqualTo("height: 100%; width: 480px"));
    }

    [Test]
    public void Send_RaisesTrimmedText_ClearsTheBox()
    {
        var sent = new List<string>();
        var cut = RenderChat([], p => p.Add(x => x.OnSendMessage, (NxChatSendMessageEventArgs e) => sent.Add(e.Message)));

        Assert.That(cut.Find(".nx-chat-send").HasAttribute("disabled"), Is.True);
        cut.Find("#chat-input").Input("  On my way  ");
        Assert.That(cut.Find(".nx-chat-send").HasAttribute("disabled"), Is.False);
        cut.Find("form").Submit();

        Assert.That(sent, Is.EqualTo(new[] { "On my way" }));
        Assert.That(cut.Find("#chat-input").GetAttribute("value") ?? "", Is.Empty);
        Assert.That(module.Invocations.Count(i => i.Identifier == "scrollToBottom"), Is.EqualTo(1));
    }

    [Test]
    public void Send_BlankOrDisabled_DoesNothing()
    {
        var sent = new List<string>();
        var cut = RenderChat([], p => p.Add(x => x.OnSendMessage, (NxChatSendMessageEventArgs e) => sent.Add(e.Message)));
        cut.Find("#chat-input").Input("   ");
        cut.Find("form").Submit();

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.That(cut.Find("#chat-input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-chat-send").HasAttribute("disabled"), Is.True);

        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void Refresh_ShowsInPlaceChanges()
    {
        var data = Conversation();
        var cut = RenderChat(data);
        data.Add(new Msg { Id = 5, Author = "me", Text = "New", Sent = Today.AddHours(8) });

        cut.InvokeAsync(cut.Instance.Refresh);

        Assert.That(cut.FindAll(".nx-chat-message"), Has.Count.EqualTo(5));
    }

    [Test]
    public void Init_TellsTheModuleTheList()
    {
        RenderChat(Conversation());
        Assert.That(module.Invocations.Count(i => i.Identifier == "init"), Is.EqualTo(1));
    }

    [Test]
    public void UnknownField_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxChat<Msg>>(p => p
            .Add(x => x.Data, Conversation())
            .Add(x => x.TextField, "Body")));
    }
}
