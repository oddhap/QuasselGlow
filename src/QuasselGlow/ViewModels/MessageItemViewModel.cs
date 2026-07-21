using Quassel.Client.Application.Text;
using Quassel.Client.Domain;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace QuasselGlow.ViewModels;

public sealed class MessageItemViewModel : ViewModelBase
{
    private const QuasselMessageType RenderedStatusTypes =
        QuasselMessageType.Nick
        | QuasselMessageType.Mode
        | QuasselMessageType.Join
        | QuasselMessageType.Part
        | QuasselMessageType.Quit
        | QuasselMessageType.Kick
        | QuasselMessageType.Kill
        | QuasselMessageType.Server
        | QuasselMessageType.Info
        | QuasselMessageType.Error
        | QuasselMessageType.DayChange
        | QuasselMessageType.Topic
        | QuasselMessageType.NetsplitJoin
        | QuasselMessageType.NetsplitQuit
        | QuasselMessageType.Invite;

    private static readonly Regex LinkRegex = new(
        @"(?<url>(?:https?://|www\.)[^\s<>""]+[^\s<>"".,;:!?])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private bool _isDaySeparatorVisible;
    private string _daySeparatorText = string.Empty;

    public MessageItemViewModel(QuasselMessage message)
    {
        Model = message;
        var sender = ParseSender(message.Sender);
        TimestampText = message.Timestamp.ToLocalTime().ToString("HH:mm");
        var cleanedContents = IrcFormattingCleaner.Clean(message.Contents);
        LineText = BuildLineText(message, sender, cleanedContents);
        SenderDisplay = IsStatus ? string.Empty : sender.DisplayNick;
        Segments = BuildSegments(LineText);
    }

    public QuasselMessage Model { get; }
    public long MessageOrder => Model.MessageId.Value;
    public string TimestampText { get; }
    public string SenderDisplay { get; }
    public string LineText { get; }
    public ReadOnlyCollection<MessageTextSegment> Segments { get; }
    public bool HasSender => !string.IsNullOrWhiteSpace(SenderDisplay);
    public int MessageColumn => HasSender ? 2 : 1;
    public int MessageColumnSpan => HasSender ? 1 : 2;
    public bool IsSelf => Model.IsSelf;
    public bool IsHighlight => Model.IsHighlight;
    public bool IsStatus => IsStatusMessage(Model);
    public bool IsDaySeparatorVisible
    {
        get => _isDaySeparatorVisible;
        private set => SetProperty(ref _isDaySeparatorVisible, value);
    }

    public string DaySeparatorText
    {
        get => _daySeparatorText;
        private set => SetProperty(ref _daySeparatorText, value);
    }

    internal void SetDaySeparator(bool isVisible, string text)
    {
        IsDaySeparatorVisible = isVisible;
        DaySeparatorText = isVisible ? text : string.Empty;
    }

    private static bool IsStatusMessage(QuasselMessage message)
    {
        return message.IsStatusMessage
            || message.Flags.HasFlag(QuasselMessageFlags.ServerMessage)
            || (message.Type & RenderedStatusTypes) != 0;
    }

    private static string BuildLineText(QuasselMessage message, SenderInfo sender, string cleanedContents)
    {
        return message.Type switch
        {
            var type when type.HasFlag(QuasselMessageType.Action) => $"* {sender.DisplayNick} {cleanedContents}".TrimEnd(),
            var type when type.HasFlag(QuasselMessageType.Nick) => BuildNickText(message, sender.DisplayNick, cleanedContents),
            var type when type.HasFlag(QuasselMessageType.Mode) => BuildModeText(message, sender.DisplayNick, cleanedContents),
            var type when type.HasFlag(QuasselMessageType.Join) => $"{sender.DisplayNick}{FormatHostmask(sender)} has joined {ResolveTarget(message, cleanedContents)}",
            var type when type.HasFlag(QuasselMessageType.Part) => AppendDetails($"{sender.DisplayNick}{FormatHostmask(sender)} has left {message.BufferInfo.BufferName}", cleanedContents),
            var type when type.HasFlag(QuasselMessageType.Quit) => AppendDetails($"{sender.DisplayNick}{FormatHostmask(sender)} has quit", cleanedContents),
            var type when type.HasFlag(QuasselMessageType.Kick) => BuildKickText(message, sender.DisplayNick, cleanedContents),
            var type when type.HasFlag(QuasselMessageType.Kill) => AppendDetails($"{sender.DisplayNick}{FormatHostmask(sender)} was killed", cleanedContents),
            var type when type.HasFlag(QuasselMessageType.DayChange) => string.IsNullOrWhiteSpace(cleanedContents)
                ? $"Day changed to {message.Timestamp.LocalDateTime:D}"
                : cleanedContents,
            var type when type.HasFlag(QuasselMessageType.NetsplitJoin) => BuildNetsplitText(cleanedContents, ended: true),
            var type when type.HasFlag(QuasselMessageType.NetsplitQuit) => BuildNetsplitText(cleanedContents, ended: false),
            _ => cleanedContents
        };
    }

    private static string BuildNickText(QuasselMessage message, string senderNick, string cleanedContents)
    {
        if (message.IsSelf)
        {
            return $"You are now known as {cleanedContents}";
        }

        return $"{senderNick} is now known as {cleanedContents}";
    }

    private static string BuildModeText(QuasselMessage message, string senderNick, string cleanedContents)
    {
        return string.IsNullOrWhiteSpace(message.Sender)
            ? $"User mode: {cleanedContents}"
            : $"Mode {cleanedContents} by {senderNick}";
    }

    private static string BuildKickText(QuasselMessage message, string senderNick, string cleanedContents)
    {
        if (string.IsNullOrWhiteSpace(cleanedContents))
        {
            return $"{senderNick} has kicked someone from {message.BufferInfo.BufferName}";
        }

        var splitIndex = cleanedContents.IndexOf(' ');
        if (splitIndex < 0)
        {
            return $"{senderNick} has kicked {cleanedContents} from {message.BufferInfo.BufferName}";
        }

        var victim = cleanedContents[..splitIndex];
        var reason = cleanedContents[(splitIndex + 1)..].Trim();
        return AppendDetails($"{senderNick} has kicked {victim} from {message.BufferInfo.BufferName}", reason);
    }

    private static string BuildNetsplitText(string cleanedContents, bool ended)
    {
        if (string.IsNullOrWhiteSpace(cleanedContents))
        {
            return ended ? "Netsplit ended" : "Netsplit";
        }

        var parts = cleanedContents
            .Split("#:#", StringSplitOptions.None)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (parts.Count < 2)
        {
            return cleanedContents;
        }

        var servers = parts[^1]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        parts.RemoveAt(parts.Count - 1);

        if (servers.Length < 2 || parts.Count == 0)
        {
            return cleanedContents;
        }

        var users = parts
            .Select(static user => ParseSender(user).DisplayNick)
            .Where(static nick => !string.IsNullOrWhiteSpace(nick));

        var summary = string.Join(", ", users);
        var prefix = ended
            ? $"Netsplit between {servers[0]} and {servers[1]} ended. Users joined"
            : $"Netsplit between {servers[0]} and {servers[1]}. Users quit";

        return string.IsNullOrWhiteSpace(summary)
            ? prefix
            : $"{prefix}: {summary}";
    }

    private static string ResolveTarget(QuasselMessage message, string cleanedContents)
    {
        return string.IsNullOrWhiteSpace(cleanedContents)
            ? message.BufferInfo.BufferName
            : cleanedContents;
    }

    private static string AppendDetails(string text, string details)
    {
        return string.IsNullOrWhiteSpace(details)
            ? text
            : $"{text} ({details})";
    }

    private static string FormatHostmask(SenderInfo sender)
    {
        return sender.HasUserAndHost
            ? $" ({sender.User}@{sender.Host})"
            : string.Empty;
    }

    private static SenderInfo ParseSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return new SenderInfo("core", string.Empty, string.Empty);
        }

        var bangIndex = sender.IndexOf('!');
        if (bangIndex <= 0)
        {
            return new SenderInfo(sender, string.Empty, string.Empty);
        }

        var nick = sender[..bangIndex];
        var userAndHost = sender[(bangIndex + 1)..];
        var atIndex = userAndHost.IndexOf('@');

        if (atIndex <= 0 || atIndex == userAndHost.Length - 1)
        {
            return new SenderInfo(nick, string.Empty, string.Empty);
        }

        return new SenderInfo(
            nick,
            userAndHost[..atIndex],
            userAndHost[(atIndex + 1)..]);
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

    private readonly record struct SenderInfo(string DisplayNick, string User, string Host)
    {
        public bool HasUserAndHost => !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Host);
    }
}
