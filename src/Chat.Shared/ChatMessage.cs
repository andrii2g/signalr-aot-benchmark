namespace Chat.Shared;

public sealed class ChatMessage
{
    public int SenderId { get; init; }
    public long Sequence { get; init; }
    public string User { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;

    // Captured by the load client before invoking the hub method.
    public long SentAtTimestamp { get; init; }
}
