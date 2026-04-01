using Avalonia.Controls;
using Avalonia.Controls.Documents;
using QuasselGlow.Controls;
using QuasselGlow.ViewModels;

namespace Quassel.Client.Application.Tests;

public sealed class MessageInlineRendererTests
{
    [Fact]
    public void Render_UsesRunsForLinksInsteadOfInlineControls()
    {
        var textBlock = new TextBlock();

        MessageInlineRenderer.SetSegments(textBlock, [
            new MessageTextSegment("watch "),
            new MessageTextSegment("https://example.com", "https://example.com"),
            new MessageTextSegment(" please")
        ]);

        var inlines = textBlock.Inlines!.ToList();

        Assert.Collection(
            inlines,
            inline => Assert.Equal("watch ", Assert.IsType<Run>(inline).Text),
            inline =>
            {
                var linkRun = Assert.IsType<Run>(inline);
                Assert.Equal("https://example.com", linkRun.Text);
                Assert.Contains("messageLink", linkRun.Classes);
            },
            inline => Assert.Equal(" please", Assert.IsType<Run>(inline).Text));

        Assert.DoesNotContain(inlines, inline => inline is InlineUIContainer);
    }
}
