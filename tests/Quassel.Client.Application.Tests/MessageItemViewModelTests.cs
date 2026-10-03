using Quassel.Client.Domain;
using QuasselGlow.ViewModels;

namespace Quassel.Client.Application.Tests;

public sealed class MessageItemViewModelTests
{
    [Fact]
    public void Constructor_ParsesHttpAndWwwLinksIntoSeparateSegments()
    {
        var viewModel = new MessageItemViewModel(CreateMessage("See https://example.com and www.example.org/test for details."));

        Assert.Equal(
            ["See ", "https://example.com", " and ", "www.example.org/test", " for details."],
            viewModel.Segments.Select(segment => segment.Text));
        Assert.Equal("https://example.com", viewModel.Segments[1].Url);
        Assert.Equal("https://www.example.org/test", viewModel.Segments[3].Url);
    }

    [Fact]
    public void Constructor_ExcludesTrailingPunctuationFromLinkSegment()
    {
        var viewModel = new MessageItemViewModel(CreateMessage("Open https://example.com/docs, please."));

        Assert.Equal(3, viewModel.Segments.Count);
        Assert.Equal("https://example.com/docs", viewModel.Segments[1].Text);
        Assert.Equal(", please.", viewModel.Segments[2].Text);
    }

    [Fact]
    public void Constructor_FormatsJoinMessagesAsStatusLines()
    {
        var viewModel = new MessageItemViewModel(CreateMessage(
            contents: "#quassel",
            type: QuasselMessageType.Join));

        Assert.True(viewModel.IsStatus);
        Assert.False(viewModel.HasSender);
        Assert.Equal("alice (user@example) has joined #quassel", viewModel.LineText);
    }

    [Fact]
    public void Constructor_FormatsPartMessagesWithReason()
    {
        var viewModel = new MessageItemViewModel(CreateMessage(
            contents: "Ping timeout: 500 seconds",
            type: QuasselMessageType.Part));

        Assert.True(viewModel.IsStatus);
        Assert.Equal("alice (user@example) has left #quassel (Ping timeout: 500 seconds)", viewModel.LineText);
    }

    [Fact]
    public void Constructor_FormatsSelfNickChanges()
    {
        var viewModel = new MessageItemViewModel(CreateMessage(
            contents: "alice_",
            type: QuasselMessageType.Nick,
            flags: QuasselMessageFlags.Self));

        Assert.True(viewModel.IsStatus);
        Assert.Equal("You are now known as alice_", viewModel.LineText);
    }

    [Fact]
    public void Constructor_FormatsNetsplitQuitMessages()
    {
        var viewModel = new MessageItemViewModel(CreateMessage(
            contents: "alice!user@example#:#bob!ident@host#:#hub.one hub.two",
            type: QuasselMessageType.NetsplitQuit,
            sender: string.Empty));

        Assert.True(viewModel.IsStatus);
        Assert.Equal("Netsplit between hub.one and hub.two. Users quit: alice, bob", viewModel.LineText);
    }

    [Fact]
    public void Constructor_ShowsUnknownControlCharacterAsVisibleControlPicture()
    {
        var viewModel = new MessageItemViewModel(CreateMessage("\u0013"));

        Assert.Equal("\u2413", viewModel.LineText);
        Assert.Single(viewModel.Segments);
        Assert.Equal("\u2413", viewModel.Segments[0].Text);
    }

    [Fact]
    public void FormattedLinksRemainClickableEvenWhenStyleChangesInsideTheUrl()
    {
        var model = new MessageItemViewModel(CreateMessage("\u0002See https://exam\u001Dple.com\u000F now"));
        Assert.Equal("See https://example.com now", model.LineText);
        Assert.Equal(model.LineText, string.Concat(model.Segments.Select(s => s.Text)));
        var links = model.Segments.Where(s => s.IsLink).ToArray();
        Assert.Equal(2, links.Length);
        Assert.All(links, s => Assert.Equal("https://example.com", s.Url));
        Assert.True(links[0].Style.Bold);
        Assert.True(links[1].Style.Italic);
        Assert.False(model.Segments[^1].Style.Bold);
    }

    [Fact]
    public void StatusMessagesKeepFormattingInReasonsAndCleanPreviewText()
    {
        var message = CreateMessage("\u000304\u0002Goodbye\u000F", QuasselMessageType.Part);
        var model = new MessageItemViewModel(message);
        Assert.Equal("alice (user@example) has left #quassel (Goodbye)", model.LineText);
        Assert.Contains(model.Segments, s => s.Text == "Goodbye" && s.Style.Bold && s.Style.Foreground == "#FF0000");
        var buffer = new BufferItemViewModel(message.BufferInfo);
        buffer.AddMessage(message, false);
        Assert.DoesNotContain('\u0003', buffer.LastMessagePreview);
        Assert.DoesNotContain("04", buffer.LastMessagePreview);
        buffer.SetChannelTopic("\u0002Welcome \u001Dhere\u000F");
        Assert.Equal("Welcome here", buffer.ChannelTopic);
        Assert.True(buffer.TopicSegments[0].Style.Bold);
        Assert.True(buffer.TopicSegments[1].Style.Italic);
    }

    private static QuasselMessage CreateMessage(
        string contents,
        QuasselMessageType type = QuasselMessageType.Plain,
        string sender = "alice!user@example",
        QuasselMessageFlags flags = QuasselMessageFlags.None)
    {
        return new QuasselMessage(
            new MsgId(1),
            DateTimeOffset.Parse("2026-03-28T12:00:00+01:00"),
            new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel"),
            type,
            contents,
            sender,
            flags);
    }
}
