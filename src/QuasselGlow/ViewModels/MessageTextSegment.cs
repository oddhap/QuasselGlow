namespace QuasselGlow.ViewModels;

public sealed class MessageTextSegment
{
    public MessageTextSegment(string text, string? url = null)
    {
        Text = text;
        Url = url;
    }

    public string Text { get; }
    public string? Url { get; }
    public bool IsLink => !string.IsNullOrWhiteSpace(Url);
}
