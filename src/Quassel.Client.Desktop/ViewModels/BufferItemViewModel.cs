using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Quassel.Client.Domain;

namespace Quassel.Client.Desktop.ViewModels;

public sealed partial class BufferItemViewModel : ViewModelBase
{
    private readonly HashSet<long> _messageIds = [];

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _hasHighlight;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _lastMessagePreview = string.Empty;

    public BufferItemViewModel(QuasselBufferInfo bufferInfo)
    {
        BufferInfo = bufferInfo;
        _displayName = bufferInfo.BufferName;
    }

    public QuasselBufferInfo BufferInfo { get; private set; }
    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];
    public bool AcceptsInput => BufferInfo.AcceptsInput;
    public bool HasUnread => UnreadCount > 0;

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
    }

    public void AddMessage(QuasselMessage message)
    {
        if (!_messageIds.Add(message.MessageId.Value))
        {
            return;
        }

        var viewModel = new MessageItemViewModel(message);
        var insertAt = Messages.TakeWhile(item => item.MessageOrder < viewModel.MessageOrder).Count();
        Messages.Insert(insertAt, viewModel);

        LastMessagePreview = BuildPreview(viewModel);
        if (!IsSelected && !message.IsSelf)
        {
            UnreadCount++;
            HasHighlight |= message.IsHighlight;
        }
    }

    public void MarkRead()
    {
        UnreadCount = 0;
        HasHighlight = false;
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
    }

    private static string BuildPreview(MessageItemViewModel message)
    {
        var content = message.LineText.Replace("\r", " ").Replace("\n", " ").Trim();
        return content.Length > 64 ? $"{content[..61]}..." : content;
    }
}
