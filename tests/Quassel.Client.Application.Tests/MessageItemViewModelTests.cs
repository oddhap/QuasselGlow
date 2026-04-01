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

    private static QuasselMessage CreateMessage(string contents)
    {
        return new QuasselMessage(
            new MsgId(1),
            DateTimeOffset.Parse("2026-03-28T12:00:00+01:00"),
            new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel"),
            QuasselMessageType.Plain,
            contents,
            "alice!user@example",
            QuasselMessageFlags.None);
    }
}
