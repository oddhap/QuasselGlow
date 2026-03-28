namespace Quassel.Client.Domain;

public sealed record QuasselMessage(
    MsgId MessageId,
    DateTimeOffset Timestamp,
    QuasselBufferInfo BufferInfo,
    QuasselMessageType Type,
    string Contents,
    string Sender,
    QuasselMessageFlags Flags)
{
    public bool IsSelf => Flags.HasFlag(QuasselMessageFlags.Self);
    public bool IsHighlight => Flags.HasFlag(QuasselMessageFlags.Highlight);
    public bool IsStatusMessage => Flags.HasFlag(QuasselMessageFlags.StatusMessage);
    public bool IsBacklog => Flags.HasFlag(QuasselMessageFlags.Backlog);
}
