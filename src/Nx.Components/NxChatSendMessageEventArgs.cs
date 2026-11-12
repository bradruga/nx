namespace Nx.Components;

/// <summary>Raised by <see cref="NxChat{TItem}.OnSendMessage"/> with what the user sent.</summary>
/// <param name="Message">The text, trimmed. Never empty.</param>
public sealed record NxChatSendMessageEventArgs(string Message);
