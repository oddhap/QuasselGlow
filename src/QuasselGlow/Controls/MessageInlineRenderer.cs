using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using QuasselGlow.ViewModels;

namespace QuasselGlow.Controls;

public sealed class MessageInlineRenderer
{
    public static readonly AttachedProperty<IEnumerable<MessageTextSegment>?> SegmentsProperty =
        AvaloniaProperty.RegisterAttached<MessageInlineRenderer, TextBlock, IEnumerable<MessageTextSegment>?>("Segments");

    static MessageInlineRenderer()
    {
        SegmentsProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Render(textBlock, GetSegments(textBlock)));
    }

    public static void SetSegments(AvaloniaObject element, IEnumerable<MessageTextSegment>? value) =>
        element.SetValue(SegmentsProperty, value);

    public static IEnumerable<MessageTextSegment>? GetSegments(AvaloniaObject element) =>
        element.GetValue(SegmentsProperty);

    private static void Render(TextBlock textBlock, IEnumerable<MessageTextSegment>? segments)
    {
        var inlines = textBlock.Inlines;
        if (inlines is null)
        {
            return;
        }

        inlines.Clear();

        foreach (var segment in segments ?? Array.Empty<MessageTextSegment>())
        {
            if (segment.IsLink && Uri.TryCreate(segment.Url, UriKind.Absolute, out var uri))
            {
                var hyperlink = new HyperlinkButton
                {
                    NavigateUri = uri,
                    Content = segment.Text
                };
                hyperlink.Classes.Add("messageLink");
                inlines.Add(new InlineUIContainer(hyperlink));
            }
            else
            {
                inlines.Add(new Run(segment.Text));
            }
        }
    }
}
