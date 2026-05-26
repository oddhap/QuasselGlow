using Quassel.Client.Domain;
using Quassel.Client.Infrastructure;

namespace Quassel.Client.Application.Tests;

public sealed class LocalBufferMessageCacheStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "quassel-message-cache-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void StoreAndLoadMessages_RoundTripsLatestMessagesForBuffer()
    {
        Directory.CreateDirectory(_tempDirectory);
        var store = new LocalBufferMessageCacheStore(_tempDirectory);
        var profile = new ConnectionProfile("chat.example", 4242, "alice", "secret", false);
        var bufferInfo = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var messages = new[]
        {
            CreateMessage(bufferInfo, new MsgId(1), "first"),
            CreateMessage(bufferInfo, new MsgId(2), "second"),
            CreateMessage(bufferInfo, new MsgId(3), "third")
        };

        var writeResult = store.StoreMessages(profile, bufferInfo, messages, maxMessages: 2);
        var loadResult = store.LoadMessages(profile, bufferInfo, maxMessages: 10);
        var latestResult = store.GetLatestMessageId(profile, bufferInfo.BufferId);

        Assert.Equal(MessageCacheOperationStatus.Available, writeResult.Status);
        Assert.Equal(MessageCacheOperationStatus.Available, loadResult.Status);
        Assert.Equal(["second", "third"], loadResult.Messages.Select(message => message.Contents));
        Assert.Equal(new MsgId(3), latestResult.MessageId);
        Assert.Equal(MessageCacheOperationStatus.Available, latestResult.Status);
    }

    [Fact]
    public void StoreAndLoadMessages_IsScopedPerConnectionProfile()
    {
        Directory.CreateDirectory(_tempDirectory);
        var store = new LocalBufferMessageCacheStore(_tempDirectory);
        var firstProfile = new ConnectionProfile("chat.example", 4242, "alice", "secret", false);
        var secondProfile = new ConnectionProfile("chat.example", 4242, "bob", "secret", false);
        var bufferInfo = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        store.StoreMessages(firstProfile, bufferInfo, [CreateMessage(bufferInfo, new MsgId(5), "alice history")]);

        Assert.Single(store.LoadMessages(firstProfile, bufferInfo, 10).Messages);
        Assert.Empty(store.LoadMessages(secondProfile, bufferInfo, 10).Messages);
    }

    [Fact]
    public void LoadMessages_InvalidPersistedJson_ReturnsDegradedStatus()
    {
        Directory.CreateDirectory(_tempDirectory);
        var store = new LocalBufferMessageCacheStore(_tempDirectory);
        var profile = new ConnectionProfile("chat.example", 4242, "alice", "secret", false);
        var bufferInfo = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        store.StoreMessages(profile, bufferInfo, [CreateMessage(bufferInfo, new MsgId(5), "alice history")]);
        var cacheFile = Directory.GetFiles(_tempDirectory, "1.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(cacheFile, "{not json");

        var result = store.LoadMessages(profile, bufferInfo, 10);

        Assert.Equal(MessageCacheOperationStatus.Degraded, result.Status);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void StoreMessages_UnwritableCachePath_ReturnsDegradedStatus()
    {
        Directory.CreateDirectory(_tempDirectory);
        var blockerPath = Path.Combine(_tempDirectory, "blocker");
        File.WriteAllText(blockerPath, "not a directory");
        var store = new LocalBufferMessageCacheStore(blockerPath);
        var profile = new ConnectionProfile("chat.example", 4242, "alice", "secret", false);
        var bufferInfo = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        var result = store.StoreMessages(profile, bufferInfo, [CreateMessage(bufferInfo, new MsgId(5), "alice history")]);

        Assert.Equal(MessageCacheOperationStatus.Degraded, result.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static QuasselMessage CreateMessage(QuasselBufferInfo bufferInfo, MsgId messageId, string contents)
    {
        return new QuasselMessage(
            messageId,
            DateTimeOffset.Parse("2026-04-08T20:00:00+02:00"),
            bufferInfo,
            QuasselMessageType.Plain,
            contents,
            "alice!user@example",
            QuasselMessageFlags.Backlog);
    }
}
