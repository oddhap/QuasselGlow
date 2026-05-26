using Quassel.Client.Domain;

namespace Quassel.Client.Infrastructure;

public enum MessageCacheOperationStatus
{
    Available,
    Degraded
}

public sealed record MessageCacheOperationResult(
    MessageCacheOperationStatus Status,
    string? Detail = null)
{
    public static MessageCacheOperationResult Available()
    {
        return new MessageCacheOperationResult(MessageCacheOperationStatus.Available);
    }

    public static MessageCacheOperationResult Degraded(string? detail = null)
    {
        return new MessageCacheOperationResult(MessageCacheOperationStatus.Degraded, detail);
    }
}

public sealed record MessageCacheLoadResult(
    IReadOnlyList<QuasselMessage> Messages,
    MessageCacheOperationStatus Status,
    string? Detail = null)
{
    public MessageCacheOperationResult ToOperationResult()
    {
        return new MessageCacheOperationResult(Status, Detail);
    }
}

public sealed record MessageCacheLatestMessageResult(
    MsgId MessageId,
    MessageCacheOperationStatus Status,
    string? Detail = null)
{
    public MessageCacheOperationResult ToOperationResult()
    {
        return new MessageCacheOperationResult(Status, Detail);
    }
}
