namespace Nx.Components;

/// <summary>The context of <see cref="NxChat{TItem}.MessageContentTemplate"/>: one message.</summary>
/// <param name="Message">The message's data item.</param>
/// <param name="IsOwn">Sent by <see cref="NxChat{TItem}.AuthorId"/>, shown on the right.</param>
/// <param name="IsTyping">The item is a typing indicator (<see cref="NxChat{TItem}.IsTypingField"/>).</param>
public sealed record NxChatMessageContext<TItem>(TItem Message, bool IsOwn, bool IsTyping);
