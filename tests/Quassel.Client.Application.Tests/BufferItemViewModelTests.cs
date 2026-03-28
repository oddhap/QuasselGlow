using QuasselGlow.ViewModels;
using Quassel.Client.Domain;

namespace Quassel.Client.Application.Tests;

public sealed class BufferItemViewModelTests
{
    [Fact]
    public void AddMessage_QueryBuffer_SetsPrivateMessageAlert()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Query, 0, "alice");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(1), QuasselMessageFlags.None), trackUnreadState: true);

        Assert.Equal(1, viewModel.UnreadCount);
        Assert.True(viewModel.HasPrivateMessageAlert);
        Assert.False(viewModel.HasMentionAlert);
        Assert.True(viewModel.HasPriorityAlert);
    }

    [Fact]
    public void AddMessage_HighlightedChannelMessage_SetsMentionAlert()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(2), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(2), QuasselMessageFlags.Highlight), trackUnreadState: true);

        Assert.Equal(1, viewModel.UnreadCount);
        Assert.True(viewModel.HasMentionAlert);
        Assert.False(viewModel.HasPrivateMessageAlert);
        Assert.True(viewModel.HasPriorityAlert);
    }

    [Fact]
    public void AddMessage_BacklogMessage_DoesNotCreateUnreadOrAlerts()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(3), new NetworkId(1), QuasselBufferType.Query, 0, "bob");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(3), QuasselMessageFlags.Backlog | QuasselMessageFlags.Highlight), trackUnreadState: true);

        Assert.Equal(0, viewModel.UnreadCount);
        Assert.False(viewModel.HasMentionAlert);
        Assert.False(viewModel.HasPrivateMessageAlert);
        Assert.False(viewModel.HasPriorityAlert);
    }

    [Fact]
    public void MarkRead_ClearsUnreadAndAlerts()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(4), new NetworkId(1), QuasselBufferType.Query, 0, "carol");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(4), QuasselMessageFlags.Highlight), trackUnreadState: true);
        viewModel.MarkRead();

        Assert.Equal(0, viewModel.UnreadCount);
        Assert.False(viewModel.HasMentionAlert);
        Assert.False(viewModel.HasPrivateMessageAlert);
        Assert.False(viewModel.HasPriorityAlert);
    }

    [Fact]
    public void AddMessage_ChannelTopicMessage_StoresLatestChannelTopic()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(5), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(5), QuasselMessageFlags.None, QuasselMessageType.Topic, "First topic"), trackUnreadState: false);
        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(6), QuasselMessageFlags.None, QuasselMessageType.Topic, "Oddi has changed topic for #quassel to: \"Fresh topic\""), trackUnreadState: false);

        Assert.Equal("Fresh topic", viewModel.ChannelTopic);
    }

    [Fact]
    public void QueryBuffer_HidesSidebarSecondaryText()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(6), new NetworkId(1), QuasselBufferType.Query, 0, "dave");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(7), QuasselMessageFlags.None), trackUnreadState: true);

        Assert.Equal(string.Empty, viewModel.SidebarSecondaryText);
        Assert.False(viewModel.HasSidebarSecondaryText);
    }

    [Fact]
    public void ChannelBuffer_PrefersTopicInSidebarSecondaryText()
    {
        var bufferInfo = new QuasselBufferInfo(new BufferId(7), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var viewModel = new BufferItemViewModel(bufferInfo);

        viewModel.AddMessage(CreateMessage(bufferInfo, new MsgId(8), QuasselMessageFlags.None, contents: "regular preview"), trackUnreadState: true);
        viewModel.SetChannelTopic("Topic line");

        Assert.Equal("Topic line", viewModel.SidebarSecondaryText);
    }

    private static QuasselMessage CreateMessage(
        QuasselBufferInfo bufferInfo,
        MsgId messageId,
        QuasselMessageFlags flags,
        QuasselMessageType type = QuasselMessageType.Plain,
        string contents = "hello there")
    {
        return new QuasselMessage(
            messageId,
            DateTimeOffset.Parse("2026-03-28T12:00:00+01:00"),
            bufferInfo,
            type,
            contents,
            "alice!user@example",
            flags);
    }
}
