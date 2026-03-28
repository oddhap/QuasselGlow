using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Quassel.Client.Desktop.Localization;
using Quassel.Client.Domain;

namespace Quassel.Client.Desktop.ViewModels;

public sealed partial class NetworkItemViewModel : ViewModelBase
{
    private bool _usesDefaultDisplayName;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _currentServer = string.Empty;

    [ObservableProperty]
    private string _myNick = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _latency;

    [ObservableProperty]
    private int _connectionState;

    public NetworkItemViewModel(NetworkId networkId, string? displayName = null)
    {
        NetworkId = networkId;
        _usesDefaultDisplayName = string.IsNullOrWhiteSpace(displayName);
        _displayName = _usesDefaultDisplayName
            ? UiTextCatalog.Instance.Format("NetworkNameFormat", networkId.Value)
            : displayName!;
    }

    public NetworkId NetworkId { get; }
    public ObservableCollection<BufferItemViewModel> Buffers { get; } = [];

    public string StatusText => IsConnected
        ? $"{CurrentServer} | {Latency} ms"
        : UiTextCatalog.Instance["NetworkDisconnected"];

    public void Apply(QuasselNetworkState state)
    {
        if (string.IsNullOrWhiteSpace(state.NetworkName))
        {
            if (_usesDefaultDisplayName)
            {
                DisplayName = UiTextCatalog.Instance.Format("NetworkNameFormat", NetworkId.Value);
            }
        }
        else
        {
            _usesDefaultDisplayName = false;
            DisplayName = state.NetworkName;
        }

        CurrentServer = state.CurrentServer;
        MyNick = state.MyNick;
        IsConnected = state.IsConnected;
        Latency = state.Latency;
        ConnectionState = state.ConnectionState;
        OnPropertyChanged(nameof(StatusText));
    }

    public void RefreshLocalizedText(UiTextCatalog textCatalog)
    {
        if (_usesDefaultDisplayName)
        {
            DisplayName = textCatalog.Format("NetworkNameFormat", NetworkId.Value);
        }

        OnPropertyChanged(nameof(StatusText));
    }

    public void UpsertBuffer(BufferItemViewModel buffer)
    {
        var existingIndex = Buffers.IndexOf(Buffers.FirstOrDefault(item => item.BufferInfo.BufferId == buffer.BufferInfo.BufferId)!);
        if (existingIndex >= 0)
        {
            Buffers[existingIndex] = buffer;
            return;
        }

        var insertAt = Buffers.TakeWhile(item => Compare(item, buffer) <= 0).Count();
        Buffers.Insert(insertAt, buffer);
    }

    public void RemoveBuffer(BufferId bufferId)
    {
        var buffer = Buffers.FirstOrDefault(item => item.BufferInfo.BufferId == bufferId);
        if (buffer is not null)
        {
            Buffers.Remove(buffer);
        }
    }

    private static int Compare(BufferItemViewModel left, BufferItemViewModel right)
    {
        var leftRank = Rank(left.BufferInfo.Type);
        var rightRank = Rank(right.BufferInfo.Type);
        if (leftRank != rightRank)
        {
            return leftRank.CompareTo(rightRank);
        }

        return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static int Rank(QuasselBufferType type)
    {
        return type switch
        {
            QuasselBufferType.Status => 0,
            QuasselBufferType.Channel => 1,
            QuasselBufferType.Query => 2,
            QuasselBufferType.Group => 3,
            _ => 4
        };
    }
}
