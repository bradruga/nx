using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A conversation: messages from <see cref="Data"/>, the user's on the right and everyone else's on the left with an
/// avatar and name, grouped by author and day, a typing indicator, and a box that sends on <kbd>Enter</kbd>. The list
/// stays at the newest message unless the user scrolled up to read. Fields are read by lambdas.
/// </summary>
/// <example>
/// <code>
/// &lt;NxChat Data="messages" AuthorId="user" ItemAuthorId="m =&gt; m.AuthorId" ItemText="m =&gt; m.Text"
///         ItemTimestamp="m =&gt; m.Sent" OnSendMessage="SendAsync" Height="100%" /&gt;
/// </code>
/// </example>
public partial class NxChat<TItem> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-chat.js keeps the list at the bottom as messages arrive and sends on Enter (Shift+Enter is a new line).

    private const string ModulePath = "./_content/Nx.Components/nx-chat.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-chat-{Guid.NewGuid():N}"[..17];
    private IJSObjectReference? module;
    private ElementReference root;
    private ElementReference list;
    private NxTextArea? input;
    private List<Group> groups = [];
    private string? draft;

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The messages, oldest first.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>The current user's author id: messages with this <see cref="ItemAuthorId"/> are theirs, on the right.</summary>
    [Parameter] public string? AuthorId { get; set; }

    /// <summary>A message's unique id, to keep each message's markup through updates.</summary>
    [Parameter] public Func<TItem, object?>? ItemId { get; set; }

    /// <summary>A message's author id; compared with <see cref="AuthorId"/> as text.</summary>
    [Parameter] public Func<TItem, object?>? ItemAuthorId { get; set; }

    /// <summary>The author's display name, shown over their messages.</summary>
    [Parameter] public Func<TItem, string?>? ItemAuthorName { get; set; }

    /// <summary>The author's picture address; initials or an icon without one.</summary>
    [Parameter] public Func<TItem, string?>? ItemAuthorImageUrl { get; set; }

    /// <summary>The message text, shown as plain text with its line breaks.</summary>
    [Parameter] public Func<TItem, string?>? ItemText { get; set; }

    /// <summary>When it was sent (<c>DateTime</c> or <c>DateTimeOffset</c>), for times and day separators.</summary>
    [Parameter] public Func<TItem, object?>? ItemTimestamp { get; set; }

    /// <summary>Marks a message as "is typing…", shown as moving dots.</summary>
    [Parameter] public Func<TItem, bool>? ItemIsTyping { get; set; }

    // ── Look ──────────────────────────────────────────────────────────────────

    /// <summary>Content of each message's bubble in place of its text (context: the item, whether it is the user's, typing).</summary>
    [Parameter] public RenderFragment<NxChatMessageContext<TItem>>? MessageContentTemplate { get; set; }

    /// <summary>How wide a bubble can grow. Default: <see cref="NxChatMessageWidthMode.Standard"/>.</summary>
    [Parameter] public NxChatMessageWidthMode MessageWidthMode { get; set; }

    /// <summary>The chat's height; <c>100%</c> fills a container with a height. Default: <c>500px</c>.</summary>
    [Parameter] public string? Height { get; set; } = "500px";

    /// <summary>The chat's width. Default: the container's.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>The message box's placeholder. Default: "Type a message…".</summary>
    [Parameter] public string? Placeholder { get; set; } = "Type a message…";

    /// <summary>Shown while there are no messages.</summary>
    [Parameter] public string? NoDataText { get; set; } = "No messages yet";

    /// <summary>Formats messages' times and days. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Turns the message box and send button off; the messages stay readable.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The message list's accessible name. Default: "Messages".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>; the message box's is <c>{Id}-input</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes go to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised when the user sends a message; add it to <see cref="Data"/> to show it.</summary>
    [Parameter] public EventCallback<NxChatSendMessageEventArgs> OnSendMessage { get; set; }

    // ── Methods ───────────────────────────────────────────────────────────────

    /// <summary>Redraws the messages after <see cref="Data"/> changed in place.</summary>
    public void Refresh()
    {
        Build();
        StateHasChanged();
    }

    /// <summary>Moves keyboard focus to the message box.</summary>
    public ValueTask FocusAsync() => input?.FocusAsync() ?? ValueTask.CompletedTask;

    /// <summary>Scrolls to the newest message and keeps following new ones.</summary>
    public async Task ScrollToBottomAsync()
    {
        if (module is not null)
            await module.InvokeVoidAsync("scrollToBottom", list);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => $"{Id ?? generatedId}-input";
    private string InputLabel => AriaLabel is null ? "Message" : $"Message to {AriaLabel}";
    private CultureInfo Formats => Culture ?? CultureInfo.CurrentCulture;

    private string CssClass
    {
        get
        {
            var css = MessageWidthMode == NxChatMessageWidthMode.Full ? "nx-chat nx-chat-full" : "nx-chat";
            if (Disabled) css += " nx-chat-disabled";
            return string.IsNullOrEmpty(Class) ? css : $"{css} {Class}";
        }
    }

    private string? OuterStyle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Height)) parts.Add($"height: {Height}");
            if (!string.IsNullOrEmpty(Width)) parts.Add($"width: {Width}");
            if (!string.IsNullOrEmpty(Style)) parts.Add(Style);
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }
    }

    private string DayText(DateTime day)
    {
        var today = DateTime.Today;
        if (day == today) return "Today";
        if (day == today.AddDays(-1)) return "Yesterday";
        return day.ToString(day.Year == today.Year ? "dddd, MMMM d" : "D", Formats);
    }

    private string TimeText(DateTime time) => time.ToString("t", Formats);

    private string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var letters = words.Length == 1 ? words[0][..1] : $"{words[0][0]}{words[^1][0]}";
        return letters.ToUpper(Formats);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet() => Build();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is not null)
            await module.InvokeVoidAsync("init", root, list);
    }

    // Consecutive messages of one author on one day share an avatar, a name and a time.
    private void Build()
    {
        var next = new List<Group>();
        DateTime? lastDay = null;
        foreach (var item in Data ?? [])
        {
            if (item is null) continue;
            var author = ItemAuthorId?.Invoke(item)?.ToString();
            var sent = ItemTimestamp?.Invoke(item) switch
            {
                DateTime d => d,
                DateTimeOffset o => o.LocalDateTime,
                _ => (DateTime?)null
            };
            var typing = ItemIsTyping?.Invoke(item) is true;
            var dayBreak = sent is { } s && s.Date != lastDay;
            if (sent is { } at) lastDay = at.Date;
            var group = next.Count > 0 ? next[^1] : null;
            if (group is null || group.AuthorId != author || dayBreak)
            {
                group = new Group(author, author is not null && author == AuthorId, ItemAuthorName?.Invoke(item),
                    ItemAuthorImageUrl?.Invoke(item), dayBreak ? sent!.Value.Date : null);
                next.Add(group);
            }
            var key = ItemId?.Invoke(item) ?? (object)item;
            group.Messages.Add(new Message(item, key, ItemText?.Invoke(item), typing));
            if (sent is not null && !typing) group.Time = sent;
        }
        groups = next;
    }

    // ── Sending ───────────────────────────────────────────────────────────────

    private async Task SendAsync()
    {
        var text = draft?.Trim();
        if (Disabled || string.IsNullOrEmpty(text))
            return;
        draft = "";
        // Follow from now on: the handler may take a while (an answer), and the user may scroll up meanwhile.
        await ScrollToBottomAsync();
        await OnSendMessage.InvokeAsync(new NxChatSendMessageEventArgs(text));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", root);
            await module.DisposeAsync();
        }
        catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }

    private sealed record Message(TItem Item, object Key, string? Text, bool Typing);

    private sealed class Group(string? authorId, bool own, string? name, string? imageUrl, DateTime? day)
    {
        public string? AuthorId { get; } = authorId;
        public bool Own { get; } = own;
        public string? Name { get; } = name;
        public string? ImageUrl { get; } = imageUrl;
        public DateTime? Day { get; } = day;
        public DateTime? Time { get; set; }
        public List<Message> Messages { get; } = [];
    }
}
