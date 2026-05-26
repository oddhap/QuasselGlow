using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quassel.Client.Domain;

namespace Quassel.Client.Infrastructure;

public sealed class LocalBufferMessageCacheStore
{
    private const int DefaultMessageLimit = 400;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly string _cacheRootPath;

    public LocalBufferMessageCacheStore(string? cacheRootPath = null)
    {
        if (!string.IsNullOrWhiteSpace(cacheRootPath))
        {
            _cacheRootPath = cacheRootPath;
            return;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheRootPath = Path.Combine(localAppData, "QuasselGlow", "message-cache");
    }

    public MessageCacheLoadResult LoadMessages(ConnectionProfile profile, QuasselBufferInfo bufferInfo, int maxMessages = 150)
    {
        if (!bufferInfo.BufferId.IsValid || maxMessages <= 0)
        {
            return new MessageCacheLoadResult([], MessageCacheOperationStatus.Available);
        }

        try
        {
            var persisted = LoadPersistedBuffer(profile, bufferInfo.BufferId);
            if (persisted.Messages.Count == 0)
            {
                return new MessageCacheLoadResult([], MessageCacheOperationStatus.Available);
            }

            var messages = persisted.Messages
                .OrderBy(message => message.MessageId)
                .TakeLast(maxMessages)
                .Select(message => message.ToDomain(bufferInfo))
                .ToArray();

            return new MessageCacheLoadResult(messages, MessageCacheOperationStatus.Available);
        }
        catch (Exception ex)
        {
            return new MessageCacheLoadResult([], MessageCacheOperationStatus.Degraded, ex.Message);
        }
    }

    public MessageCacheLatestMessageResult GetLatestMessageId(ConnectionProfile profile, BufferId bufferId)
    {
        if (!bufferId.IsValid)
        {
            return new MessageCacheLatestMessageResult(new MsgId(-1), MessageCacheOperationStatus.Available);
        }

        try
        {
            var persisted = LoadPersistedBuffer(profile, bufferId);
            var messageId = persisted.Messages.Count == 0
                ? new MsgId(-1)
                : new MsgId(persisted.Messages.Max(message => message.MessageId));

            return new MessageCacheLatestMessageResult(messageId, MessageCacheOperationStatus.Available);
        }
        catch (Exception ex)
        {
            return new MessageCacheLatestMessageResult(new MsgId(-1), MessageCacheOperationStatus.Degraded, ex.Message);
        }
    }

    public MessageCacheOperationResult StoreMessages(ConnectionProfile profile, QuasselBufferInfo bufferInfo, IReadOnlyList<QuasselMessage> messages, int maxMessages = DefaultMessageLimit)
    {
        if (!bufferInfo.BufferId.IsValid || messages.Count == 0)
        {
            return MessageCacheOperationResult.Available();
        }

        try
        {
            var persisted = LoadPersistedBuffer(profile, bufferInfo.BufferId);
            MergeMessages(persisted.Messages, messages);
            TrimMessages(persisted.Messages, maxMessages);
            SavePersistedBuffer(profile, bufferInfo.BufferId, persisted);
            return MessageCacheOperationResult.Available();
        }
        catch (Exception ex)
        {
            return MessageCacheOperationResult.Degraded(ex.Message);
        }
    }

    public MessageCacheOperationResult AppendMessage(ConnectionProfile profile, QuasselMessage message, int maxMessages = DefaultMessageLimit)
    {
        if (!message.BufferInfo.BufferId.IsValid || !message.MessageId.IsValid)
        {
            return MessageCacheOperationResult.Available();
        }

        try
        {
            var persisted = LoadPersistedBuffer(profile, message.BufferInfo.BufferId);
            MergeMessages(persisted.Messages, [message]);
            TrimMessages(persisted.Messages, maxMessages);
            SavePersistedBuffer(profile, message.BufferInfo.BufferId, persisted);
            return MessageCacheOperationResult.Available();
        }
        catch (Exception ex)
        {
            return MessageCacheOperationResult.Degraded(ex.Message);
        }
    }

    public MessageCacheOperationResult RemoveBuffer(ConnectionProfile profile, BufferId bufferId)
    {
        if (!bufferId.IsValid)
        {
            return MessageCacheOperationResult.Available();
        }

        try
        {
            var filePath = GetBufferFilePath(profile, bufferId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return MessageCacheOperationResult.Available();
        }
        catch (Exception ex)
        {
            return MessageCacheOperationResult.Degraded(ex.Message);
        }
    }

    private PersistedBuffer LoadPersistedBuffer(ConnectionProfile profile, BufferId bufferId)
    {
        var filePath = GetBufferFilePath(profile, bufferId);
        if (!File.Exists(filePath))
        {
            return new PersistedBuffer();
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PersistedBuffer>(json, SerializerOptions) ?? new PersistedBuffer();
    }

    private void SavePersistedBuffer(ConnectionProfile profile, BufferId bufferId, PersistedBuffer persisted)
    {
        var filePath = GetBufferFilePath(profile, bufferId);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(persisted, SerializerOptions);
        File.WriteAllText(filePath, json);
    }

    private string GetBufferFilePath(ConnectionProfile profile, BufferId bufferId)
    {
        return Path.Combine(_cacheRootPath, BuildScopeKey(profile), $"{bufferId.Value}.json");
    }

    private static string BuildScopeKey(ConnectionProfile profile)
    {
        var normalized = $"{profile.Host.Trim().ToLowerInvariant()}|{profile.Port}|{profile.Username.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void MergeMessages(List<PersistedMessage> target, IReadOnlyList<QuasselMessage> messages)
    {
        var byId = target.ToDictionary(message => message.MessageId);
        foreach (var message in messages)
        {
            if (!message.MessageId.IsValid)
            {
                continue;
            }

            byId[message.MessageId.Value] = PersistedMessage.FromDomain(message);
        }

        target.Clear();
        target.AddRange(byId.Values.OrderBy(message => message.MessageId));
    }

    private static void TrimMessages(List<PersistedMessage> messages, int maxMessages)
    {
        if (maxMessages <= 0 || messages.Count <= maxMessages)
        {
            return;
        }

        messages.RemoveRange(0, messages.Count - maxMessages);
    }

    private sealed class PersistedBuffer
    {
        public List<PersistedMessage> Messages { get; init; } = [];
    }

    private sealed class PersistedMessage
    {
        public long MessageId { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public int Type { get; init; }
        public string Contents { get; init; } = string.Empty;
        public string Sender { get; init; } = string.Empty;
        public int Flags { get; init; }

        public static PersistedMessage FromDomain(QuasselMessage message)
        {
            return new PersistedMessage
            {
                MessageId = message.MessageId.Value,
                Timestamp = message.Timestamp,
                Type = (int)message.Type,
                Contents = message.Contents,
                Sender = message.Sender,
                Flags = (int)message.Flags
            };
        }

        public QuasselMessage ToDomain(QuasselBufferInfo bufferInfo)
        {
            return new QuasselMessage(
                new MsgId(MessageId),
                Timestamp,
                bufferInfo,
                (QuasselMessageType)Type,
                Contents,
                Sender,
                (QuasselMessageFlags)Flags);
        }
    }
}
