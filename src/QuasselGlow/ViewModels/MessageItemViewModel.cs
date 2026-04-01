using Quassel.Client.Application.Text;
using Quassel.Client.Domain;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace QuasselGlow.ViewModels;

public sealed class MessageItemViewModel : ViewModelBase
{
    private static readonly Regex LinkRegex = new(
        @"(?<url>(?:https?://|www\.)[^\s<>""]+[^\s<>"".,;:!?])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MessageItemViewModel(QuasselMessage message)
    {
        Model = message;
        SenderDisplay = ExtractNick(message.Sender);
        TimestampText = message.Timestamp.ToLocalTime().ToString("HH:mm");
        var cleanedContents = IrcFormattingCleaner.Clean(message.Contents);
        LineText = message.Type.HasFlag(QuasselMessageType.Action)
            ? $"* {SenderDisplay} {cleanedContents}"
            : cleanedContents;
        Segments = BuildSegments(LineText);
    }

    public QuasselMessage Model { get; }
    public long MessageOrder => Model.MessageId.Value;
    public string TimestampText { get; }
    public string SenderDisplay { get; }
    public string LineText { get; }
    public ReadOnlyCollection<MessageTextSegment> Segments { get; }
    public bool IsSelf => Model.IsSelf;
    public bool IsHighlight => Model.IsHighlight;
    public bool IsStatus => Model.IsStatusMessage || Model.Type.HasFlag(QuasselMessageType.Info) || Model.Type.HasFlag(QuasselMessageType.Error);

    private static string ExtractNick(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return "core";
        }

        var bangIndex = sender.IndexOf('!');
        return bangIndex > 0 ? sender[..bangIndex] : sender;
    }

    private static ReadOnlyCollection<MessageTextSegment> BuildSegments(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
        {
            return Array.AsReadOnly(Array.Empty<MessageTextSegment>());
        }

        var segments = new List<MessageTextSegment>();
        var currentIndex = 0;

        foreach (Match match in LinkRegex.Matches(lineText))
        {
            if (!match.Success)
            {
                continue;
            }

            if (match.Index > currentIndex)
            {
                segments.Add(new MessageTextSegment(lineText[currentIndex..match.Index]));
            }

            var linkText = match.Groups["url"].Value;
            var normalizedUrl = linkText.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? $"https://{linkText}"
                : linkText;
            segments.Add(new MessageTextSegment(linkText, normalizedUrl));
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < lineText.Length)
        {
            segments.Add(new MessageTextSegment(lineText[currentIndex..]));
        }

        return segments.AsReadOnly();
    }
}
