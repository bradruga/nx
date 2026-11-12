namespace Nx.Demo.Data;

/// <summary>A chat message as the demos keep it; NxChat reads it by field name.</summary>
public sealed class ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AuthorId { get; init; } = "";
    public string? AuthorName { get; init; }
    public string? Text { get; set; }
    public DateTime Sent { get; init; } = DateTime.Now;
    public bool IsTyping { get; set; }
    public List<string> Choices { get; init; } = [];
}

/// <summary>Conversations for the chat demos.</summary>
public static class DemoChat
{
    public const string Me = "me";

    public static List<ChatMessage> Crew()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var today = DateTime.Today;
        return
        [
            new() { AuthorId = "dana", AuthorName = "Dana Ortiz", Text = "WP-12 stripped clean. Sending the inspection sheet tonight.", Sent = yesterday.AddHours(16).AddMinutes(5) },
            new() { AuthorId = Me, Text = "Thanks. Any bug holes on the reveal?", Sent = yesterday.AddHours(16).AddMinutes(9) },
            new() { AuthorId = "dana", AuthorName = "Dana Ortiz", Text = "Two small ones, patched.", Sent = yesterday.AddHours(16).AddMinutes(12) },
            new() { AuthorId = "sam", AuthorName = "Sam Lee", Text = "Trucks for the Route 9 pour are confirmed for 6:30.", Sent = today.AddHours(7).AddMinutes(2) },
            new() { AuthorId = "sam", AuthorName = "Sam Lee", Text = "Three loads first, then every 40 minutes.\nCall me if the slump is off.", Sent = today.AddHours(7).AddMinutes(3) },
            new() { AuthorId = Me, Text = "Got it. I'll be at the plant by 6.", Sent = today.AddHours(7).AddMinutes(10) },
        ];
    }

    /// <summary>The assistant's canned answer to a question, with follow-up choices.</summary>
    public static (string Text, List<string> Choices) Answer(string question) =>
        question.Contains("pour", StringComparison.OrdinalIgnoreCase)
            ? ("The next pour is <strong>P-14</strong> on Thursday at 7:00: 42 yd³ of 5000 psi, three trucks.", ["Who is on the crew?", "Show the mix design"])
            : question.Contains("crew", StringComparison.OrdinalIgnoreCase)
                ? ("Dana Ortiz (lead), Sam Lee and Priya Nair are on P-14.", ["When is the next pour?"])
                : ("I can answer about pours and crews in this demo. Try one of these:", ["When is the next pour?", "Who is on the crew?"]);
}
