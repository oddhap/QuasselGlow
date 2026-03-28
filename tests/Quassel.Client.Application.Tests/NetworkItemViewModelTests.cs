using QuasselGlow.ViewModels;
using Quassel.Client.Domain;

namespace Quassel.Client.Application.Tests;

public sealed class NetworkItemViewModelTests
{
    [Fact]
    public void Buffers_ReorderWhenUnreadOrPriorityChanges()
    {
        var network = new NetworkItemViewModel(new NetworkId(1), "Libera");
        var status = new BufferItemViewModel(new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Status, 0, "status"));
        var channel = new BufferItemViewModel(new QuasselBufferInfo(new BufferId(2), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel"));
        var query = new BufferItemViewModel(new QuasselBufferInfo(new BufferId(3), new NetworkId(1), QuasselBufferType.Query, 0, "alice"));

        network.UpsertBuffer(status);
        network.UpsertBuffer(channel);
        network.UpsertBuffer(query);

        query.AddMessage(CreateMessage(query.BufferInfo, new MsgId(1), QuasselMessageFlags.None), trackUnreadState: true);

        Assert.Equal("alice", network.Buffers[0].DisplayName);

        channel.AddMessage(CreateMessage(channel.BufferInfo, new MsgId(2), QuasselMessageFlags.Highlight), trackUnreadState: true);

        Assert.Equal("#quassel", network.Buffers[0].DisplayName);
        Assert.Equal("alice", network.Buffers[1].DisplayName);
    }

    [Fact]
    public void Buffers_DoNotReorderWhenSelectionChanges()
    {
        var network = new NetworkItemViewModel(new NetworkId(1), "Libera");
        var channel = new BufferItemViewModel(new QuasselBufferInfo(new BufferId(2), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel"));
        var query = new BufferItemViewModel(new QuasselBufferInfo(new BufferId(3), new NetworkId(1), QuasselBufferType.Query, 0, "alice"));

        network.UpsertBuffer(channel);
        network.UpsertBuffer(query);

        query.IsSelected = true;

        Assert.Equal("#quassel", network.Buffers[0].DisplayName);
        Assert.Equal("alice", network.Buffers[1].DisplayName);
    }

    private static QuasselMessage CreateMessage(QuasselBufferInfo bufferInfo, MsgId messageId, QuasselMessageFlags flags)
    {
        return new QuasselMessage(
            messageId,
            DateTimeOffset.Parse("2026-03-28T13:00:00+01:00"),
            bufferInfo,
            QuasselMessageType.Plain,
            "hello",
            "alice!user@example",
            flags);
    }
}
