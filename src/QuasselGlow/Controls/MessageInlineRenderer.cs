using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using QuasselGlow.ViewModels;

namespace QuasselGlow.Controls;

public sealed class MessageInlineRenderer
{
    private static Cursor? _linkCursor;

    public static readonly AttachedProperty<IEnumerable<MessageTextSegment>?> SegmentsProperty =
        AvaloniaProperty.RegisterAttached<MessageInlineRenderer, TextBlock, IEnumerable<MessageTextSegment>?>("Segments");

    private static readonly AttachedProperty<IReadOnlyList<LinkRange>?> LinkRangesProperty =
        AvaloniaProperty.RegisterAttached<MessageInlineRenderer, TextBlock, IReadOnlyList<LinkRange>?>("LinkRanges");

    static MessageInlineRenderer()
    {
        SegmentsProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Render(textBlock, GetSegments(textBlock)));
    }

    public static void SetSegments(AvaloniaObject element, IEnumerable<MessageTextSegment>? value) =>
        element.SetValue(SegmentsProperty, value);

    public static IEnumerable<MessageTextSegment>? GetSegments(AvaloniaObject element) =>
        element.GetValue(SegmentsProperty);

    private static void SetLinkRanges(AvaloniaObject element, IReadOnlyList<LinkRange>? value) =>
        element.SetValue(LinkRangesProperty, value);

    private static IReadOnlyList<LinkRange>? GetLinkRanges(AvaloniaObject element) =>
        element.GetValue(LinkRangesProperty);

    private static void Render(TextBlock textBlock, IEnumerable<MessageTextSegment>? segments)
    {
        var inlines = textBlock.Inlines;
        if (inlines is null)
        {
            return;
        }

        inlines.Clear();

        var linkRanges = new List<LinkRange>();
        var currentIndex = 0;

        foreach (var segment in segments ?? Array.Empty<MessageTextSegment>())
        {
            var run = new Run(segment.Text);
            var style = segment.Style;
            if (style.Bold) run.FontWeight = FontWeight.Bold;
            if (style.Italic) run.FontStyle = FontStyle.Italic;
            if (style.Monospace && Avalonia.Application.Current?.TryFindResource("MonoFontFamily", out var mono) == true && mono is FontFamily font)
                run.FontFamily = font;
            var decorations = new TextDecorationCollection();
            if (style.Underline || segment.IsLink) decorations.Add(TextDecorations.Underline[0]);
            if (style.Strikethrough) decorations.Add(TextDecorations.Strikethrough[0]);
            if (decorations.Count > 0) run.TextDecorations = decorations;
            if (style.Foreground is { } foreground) run.Foreground = Brush.Parse(foreground);
            if (style.Background is { } background) run.Background = Brush.Parse(background);
            if (style.Reverse)
            {
                var ink = run.Foreground ?? textBlock.Foreground;
                var paper = run.Background;
                if (paper is null && Avalonia.Application.Current?.TryFindResource("ShellPanel", out var panel) == true)
                    paper = panel as IBrush;
                run.Foreground = paper ?? Brushes.White;
                run.Background = ink ?? Brushes.Black;
            }
            if (segment.IsLink && Uri.TryCreate(segment.Url, UriKind.Absolute, out var uri))
            {
                // Explicit IRC colors take precedence over the normal link color.
                if (style.Foreground is null && !style.Reverse) run.Classes.Add("messageLink");
                linkRanges.Add(new LinkRange(currentIndex, segment.Text.Length, uri));
            }
            inlines.Add(run);

            currentIndex += segment.Text.Length;
        }

        SetLinkRanges(textBlock, linkRanges.Count == 0 ? null : linkRanges.AsReadOnly());
        textBlock.PointerReleased -= OnPointerReleased;
        textBlock.PointerMoved -= OnPointerMoved;
        textBlock.PointerExited -= OnPointerExited;

        if (linkRanges.Count > 0)
        {
            textBlock.PointerReleased += OnPointerReleased;
            textBlock.PointerMoved += OnPointerMoved;
            textBlock.PointerExited += OnPointerExited;
        }
        else
        {
            textBlock.Cursor = null;
        }
    }

    private static async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not TextBlock textBlock
            || e.InitialPressMouseButton != MouseButton.Left
            || !TryGetLinkAtPosition(textBlock, e.GetPosition(textBlock), out var uri))
        {
            return;
        }

        var launcher = TopLevel.GetTopLevel(textBlock)?.Launcher;
        if (launcher is null)
        {
            return;
        }

        e.Handled = true;
        await launcher.LaunchUriAsync(uri);
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Cursor = TryGetLinkAtPosition(textBlock, e.GetPosition(textBlock), out _)
            ? GetLinkCursor()
            : null;
    }

    private static void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            textBlock.Cursor = null;
        }
    }

    private static bool TryGetLinkAtPosition(TextBlock textBlock, Point position, out Uri uri)
    {
        uri = null!;

        var linkRanges = GetLinkRanges(textBlock);
        if (linkRanges is null || linkRanges.Count == 0)
        {
            return false;
        }

        var point = position;
        var hit = textBlock.TextLayout.HitTestPoint(in point);
        if (!hit.IsInside)
        {
            return false;
        }

        var textPosition = hit.TextPosition;
        if (hit.IsTrailing && textPosition > 0)
        {
            textPosition--;
        }

        foreach (var linkRange in linkRanges)
        {
            if (textPosition >= linkRange.Start && textPosition < linkRange.End)
            {
                uri = linkRange.Uri;
                return true;
            }
        }

        return false;
    }

    private static Cursor? GetLinkCursor()
    {
        if (_linkCursor is not null)
        {
            return _linkCursor;
        }

        try
        {
            _linkCursor = new Cursor(StandardCursorType.Hand);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return _linkCursor;
    }

    private sealed record LinkRange(int Start, int Length, Uri Uri)
    {
        public int End => Start + Length;
    }
}
