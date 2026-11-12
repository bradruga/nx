namespace Nx.Components;

/// <summary>How wide an <see cref="NxChat{TItem}"/> message's bubble can grow.</summary>
public enum NxChatMessageWidthMode
{
    /// <summary>Up to three quarters of the chat (<c>--nx-chat-bubble-width</c>).</summary>
    Standard,

    /// <summary>The whole width, for long answers, tables and code.</summary>
    Full
}
