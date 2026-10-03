using Quassel.Client.Domain;

namespace Quassel.Client.Infrastructure;

public enum DccChatState { Disconnected, Connecting, Connected, Error }

public interface IDccChatSession : IAsyncDisposable
{
    event Action<string>? TextReceived;
    event Action<DccChatState, string?>? StateChanged;
    Task ConnectAsync(DccChatOffer offer, CancellationToken cancellationToken = default);
    Task SendLineAsync(string text, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
