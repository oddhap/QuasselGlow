using Quassel.Client.Domain;

namespace Quassel.Client.Infrastructure;

public interface IQuasselSessionService : IAsyncDisposable
{
    event Action<QuasselConnectionState, string?>? ConnectionStateChanged;
    event Action<QuasselSessionState>? SessionStateReceived;
    event Action<QuasselNetworkState>? NetworkStateReceived;
    event Action<QuasselBufferInfo>? BufferInfoUpdated;
    event Action<QuasselChannelState>? ChannelStateReceived;
    event Action<QuasselChannelTopicUpdate>? ChannelTopicReceived;
    event Action<QuasselMessage>? MessageReceived;
    event Action<string>? StatusReceived;
    event Action<NetworkId>? NetworkRemoved;

    Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendInputAsync(QuasselBufferInfo bufferInfo, string text, CancellationToken cancellationToken = default);
    Task EnsureBacklogAsync(QuasselBufferInfo bufferInfo, int amount = 120, CancellationToken cancellationToken = default);
}
