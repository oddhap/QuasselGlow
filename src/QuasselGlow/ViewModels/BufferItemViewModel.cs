using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Quassel.Client.Application.Text;
using Quassel.Client.Domain;

namespace QuasselGlow.ViewModels;

public sealed partial class BufferItemViewModel : ViewModelBase
{
    private static readonly Regex TopicChangePattern = new(@"\bto:\s*""(?<topic>.*)""\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TopicChangeSingleQuotePattern = new(@"\bto:\s*'(?<topic>.*)'\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HashSet<long> _messageIds = [];
    private long _latestTopicMessageOrder = long.MinValue;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _hasMentionAlert;

    [ObservableProperty]
    private bool _hasPrivateMessageAlert;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _lastMessagePreview = string.Empty;

    [ObservableProperty]
    private string _channelTopic = string.Empty;

    public BufferItemViewModel(QuasselBufferInfo bufferInfo)
    {
        BufferInfo = bufferInfo;
        _displayName = bufferInfo.BufferName;
    }

    public QuasselBufferInfo BufferInfo { get; private set; }
    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];
    public ObservableCollection<ChannelUserViewModel> ChannelUsers { get; } = [];
    public bool AcceptsInput => BufferInfo.AcceptsInput;
    public bool HasUnread => UnreadCount > 0;
    public bool HasPriorityAlert => HasMentionAlert || HasPrivateMessageAlert;
    public int MemberCount => ChannelUsers.Count;
    public bool HasChannelUsers => MemberCount > 0;
    public string SidebarSecondaryText => BufferInfo.Type switch
    {
        QuasselBufferType.Query => string.Empty,
        QuasselBufferType.Channel when !string.IsNullOrWhiteSpace(ChannelTopic) => ChannelTopic,
        _ => LastMessagePreview
    };
    public bool HasSidebarSecondaryText => !string.IsNullOrWhiteSpace(SidebarSecondaryText);

    public string Prefix => BufferInfo.Type switch
    {
        QuasselBufferType.Channel => "#",
        QuasselBufferType.Query => "@",
        QuasselBufferType.Status => ">",
        QuasselBufferType.Group => "+",
        _ => "-"
    };

    public void UpdateInfo(QuasselBufferInfo info)
    {
        BufferInfo = info;
        DisplayName = info.BufferName;
        OnPropertyChanged(nameof(BufferInfo));
        OnPropertyChanged(nameof(AcceptsInput));
        OnPropertyChanged(nameof(Prefix));
        OnPropertyChanged(nameof(SidebarSecondaryText));
        OnPropertyChanged(nameof(HasSidebarSecondaryText));
    }

    public void AddMessage(QuasselMessage message, bool trackUnreadState)
    {
        if (!_messageIds.Add(message.MessageId.Value))
        {
            return;
        }

        var viewModel = new MessageItemViewModel(message);
        var insertAt = Messages.TakeWhile(item => item.MessageOrder < viewModel.MessageOrder).Count();
        Messages.Insert(insertAt, viewModel);

        LastMessagePreview = BuildPreview(viewModel);
        UpdateChannelTopic(message, viewModel.MessageOrder);
        if (!trackUnreadState || message.IsSelf || message.IsBacklog)
        {
            return;
        }

        UnreadCount++;
        if (message.IsHighlight)
        {
            HasMentionAlert = true;
        }

        if (BufferInfo.Type == QuasselBufferType.Query && !message.IsStatusMessage)
        {
            HasPrivateMessageAlert = true;
        }
    }

    public void MarkRead()
    {
        UnreadCount = 0;
        HasMentionAlert = false;
        HasPrivateMessageAlert = false;
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
    }

    partial void OnLastMessagePreviewChanged(string value)
    {
        OnPropertyChanged(nameof(SidebarSecondaryText));
        OnPropertyChanged(nameof(HasSidebarSecondaryText));
    }

    partial void OnChannelTopicChanged(string value)
    {
        OnPropertyChanged(nameof(SidebarSecondaryText));
        OnPropertyChanged(nameof(HasSidebarSecondaryText));
    }

    partial void OnHasMentionAlertChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPriorityAlert));
    }

    partial void OnHasPrivateMessageAlertChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPriorityAlert));
    }

    private void UpdateChannelTopic(QuasselMessage message, long messageOrder)
    {
        if (BufferInfo.Type != QuasselBufferType.Channel || !message.Type.HasFlag(QuasselMessageType.Topic))
        {
            return;
        }

        if (messageOrder < _latestTopicMessageOrder)
        {
            return;
        }

        var cleanedTopic = ExtractTopicFromMessageContents(message.Contents);
        if (string.IsNullOrWhiteSpace(cleanedTopic))
        {
            return;
        }

        _latestTopicMessageOrder = messageOrder;
        SetChannelTopic(cleanedTopic);
    }

    public void SetChannelTopic(string topic)
    {
        var cleanedTopic = IrcFormattingCleaner.Clean(topic).Trim();
        ChannelTopic = cleanedTopic;
    }

    public void ApplyChannelState(QuasselChannelState state)
    {
        if (BufferInfo.Type != QuasselBufferType.Channel)
        {
            return;
        }

        SetChannelTopic(state.Topic);
        ReplaceChannelUsers(state.Users);
    }

    private void ReplaceChannelUsers(IReadOnlyList<QuasselChannelUser> users)
    {
        var sortedUsers = users
            .Select(user => new ChannelUserViewModel(user))
            .OrderBy(user => user, Comparer<ChannelUserViewModel>.Create(ChannelUserViewModel.Compare))
            .ToArray();

        ChannelUsers.Clear();
        foreach (var user in sortedUsers)
        {
            ChannelUsers.Add(user);
        }

        OnPropertyChanged(nameof(MemberCount));
        OnPropertyChanged(nameof(HasChannelUsers));
    }

    private static string ExtractTopicFromMessageContents(string contents)
    {
        var cleanedContents = IrcFormattingCleaner.Clean(contents).Trim();
        if (string.IsNullOrWhiteSpace(cleanedContents))
        {
            return string.Empty;
        }

        var quotedMatch = TopicChangePattern.Match(cleanedContents);
        if (quotedMatch.Success)
        {
            return quotedMatch.Groups["topic"].Value.Trim();
        }

        var singleQuotedMatch = TopicChangeSingleQuotePattern.Match(cleanedContents);
        if (singleQuotedMatch.Success)
        {
            return singleQuotedMatch.Groups["topic"].Value.Trim();
        }

        if (cleanedContents.Length >= 2)
        {
            var first = cleanedContents[0];
            var last = cleanedContents[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return cleanedContents[1..^1].Trim();
            }
        }

        return cleanedContents;
    }

    private static string BuildPreview(MessageItemViewModel message)
    {
        var content = message.LineText.Replace("\r", " ").Replace("\n", " ").Trim();
        return content.Length > 64 ? $"{content[..61]}..." : content;
    }
}
