using Quassel.Client.Domain;
using Quassel.Client.Protocol;

namespace Quassel.Client.Infrastructure;

public sealed class QuasselSessionService : IQuasselSessionService
{
    private readonly QuasselCoreClient _client = new();
    private readonly HashSet<BufferId> _requestedBacklog = [];
    private readonly HashSet<string> _requestedChannelStates = [];

    public event Action<QuasselConnectionState, string?>? ConnectionStateChanged;
    public event Action<QuasselSessionState>? SessionStateReceived;
    public event Action<QuasselNetworkState>? NetworkStateReceived;
    public event Action<QuasselBufferInfo>? BufferInfoUpdated;
    public event Action<QuasselChannelState>? ChannelStateReceived;
    public event Action<QuasselChannelTopicUpdate>? ChannelTopicReceived;
    public event Action<QuasselMessage>? MessageReceived;
    public event Action<string>? StatusReceived;
    public event Action<NetworkId>? NetworkRemoved;

    public QuasselSessionService()
    {
        _client.ConnectionStateChanged += HandleConnectionStateChanged;
        _client.SessionStateReceived += HandleSessionStateReceived;
        _client.NetworkStateReceived += state => NetworkStateReceived?.Invoke(state);
        _client.BufferInfoUpdated += HandleBufferInfoUpdated;
        _client.ChannelStateReceived += state => ChannelStateReceived?.Invoke(state);
        _client.ChannelTopicReceived += topic => ChannelTopicReceived?.Invoke(topic);
        _client.MessageReceived += HandleMessageReceived;
        _client.StatusReceived += message => StatusReceived?.Invoke(message);
        _client.NetworkRemoved += id => NetworkRemoved?.Invoke(id);
    }

    public Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        _requestedBacklog.Clear();
        _requestedChannelStates.Clear();
        return _client.ConnectAsync(profile, cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        _requestedBacklog.Clear();
        _requestedChannelStates.Clear();
        await _client.DisconnectAsync().ConfigureAwait(false);
    }

    public Task SendInputAsync(QuasselBufferInfo bufferInfo, string text, CancellationToken cancellationToken = default)
    {
        return _client.SendInputAsync(bufferInfo, text, cancellationToken);
    }

    public Task DeleteBufferAsync(QuasselBufferInfo bufferInfo, CancellationToken cancellationToken = default)
    {
        return _client.DeleteBufferAsync(bufferInfo.BufferId, cancellationToken);
    }

    public async Task EnsureBacklogAsync(QuasselBufferInfo bufferInfo, int amount = 120, CancellationToken cancellationToken = default)
    {
        if (!_requestedBacklog.Add(bufferInfo.BufferId))
        {
            return;
        }

        await _client.RequestBacklogAsync(bufferInfo.BufferId, amount, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private void HandleConnectionStateChanged(QuasselConnectionState state, string? message)
    {
        if (state is QuasselConnectionState.Disconnected or QuasselConnectionState.Error)
        {
            _requestedBacklog.Clear();
            _requestedChannelStates.Clear();
        }

        ConnectionStateChanged?.Invoke(state, message);
    }

    private void HandleSessionStateReceived(QuasselSessionState sessionState)
    {
        SessionStateReceived?.Invoke(sessionState);

        foreach (var networkId in sessionState.Networks)
        {
            _ = _client.RequestNetworkStateAsync(networkId);
        }

        foreach (var bufferInfo in sessionState.Buffers)
        {
            _ = RequestChannelStateIfNeededAsync(bufferInfo);
        }
    }

    private void HandleBufferInfoUpdated(QuasselBufferInfo bufferInfo)
    {
        BufferInfoUpdated?.Invoke(bufferInfo);
        _ = RequestChannelStateIfNeededAsync(bufferInfo);
    }

    private void HandleMessageReceived(QuasselMessage message)
    {
        MessageReceived?.Invoke(message);

        if (message.BufferInfo.Type == QuasselBufferType.Channel
            && message.Type.HasFlag(QuasselMessageType.Nick)
            && !string.IsNullOrWhiteSpace(message.BufferInfo.BufferName))
        {
            _ = _client.RequestChannelStateAsync(message.BufferInfo.NetworkId, message.BufferInfo.BufferName);
        }
    }

    private Task RequestChannelStateIfNeededAsync(QuasselBufferInfo bufferInfo)
    {
        if (bufferInfo.Type != QuasselBufferType.Channel || string.IsNullOrWhiteSpace(bufferInfo.BufferName))
        {
            return Task.CompletedTask;
        }

        var requestKey = BuildChannelStateKey(bufferInfo.NetworkId, bufferInfo.BufferName);
        if (!_requestedChannelStates.Add(requestKey))
        {
            return Task.CompletedTask;
        }

        return _client.RequestChannelStateAsync(bufferInfo.NetworkId, bufferInfo.BufferName);
    }

    private static string BuildChannelStateKey(NetworkId networkId, string channelName)
    {
        return $"{networkId.Value}/{channelName.Trim()}";
    }
}
