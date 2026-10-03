using System.Net.Sockets;
using System.Text;
using Quassel.Client.Domain;

namespace Quassel.Client.Infrastructure;

/// <summary>A single explicitly accepted, local DCC CHAT connection, independent of the core.</summary>
public sealed class DccChatSession : IDccChatSession
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly TimeSpan _connectTimeout;
    private TcpClient? _client;
    private StreamWriter? _writer;
    private Task? _connectTask;
    private Task? _receiveTask;
    private volatile DccChatState _state;
    private int _started;
    private int _disposed;

    public DccChatSession(TimeSpan? connectTimeout = null)
    {
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(15);
    }

    public event Action<string>? TextReceived;
    public event Action<DccChatState, string?>? StateChanged;

    public Task ConnectAsync(DccChatOffer offer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("This DCC session has already been started.");
        }

        _connectTask = ConnectCoreAsync(offer, cancellationToken);
        return _connectTask;
    }

    private async Task ConnectCoreAsync(DccChatOffer offer, CancellationToken cancellationToken)
    {
        SetState(DccChatState.Connecting);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        timeout.CancelAfter(_connectTimeout);
        try
        {
            _client = new TcpClient(offer.Address.AddressFamily) { NoDelay = true };
            await _client.ConnectAsync(offer.Address, offer.Port, timeout.Token).ConfigureAwait(false);
            _lifetime.Token.ThrowIfCancellationRequested();
            var stream = _client.GetStream();
            _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n", AutoFlush = true };
            SetState(DccChatState.Connected);
            _receiveTask = ReceiveAsync(stream);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            _client?.Dispose();
            SetState(DccChatState.Disconnected);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            _client?.Dispose();
            SetState(DccChatState.Error, ex is OperationCanceledException ? "Connection timed out." : ex.Message);
        }
    }

    private async Task ReceiveAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var buffer = new char[2048];
        try
        {
            while (true)
            {
                // Read chunks rather than lines: MUD prompts may have no trailing newline.
                var count = await reader.ReadAsync(buffer.AsMemory(), _lifetime.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                TextReceived?.Invoke(new string(buffer, 0, count));
            }

            if (_state != DccChatState.Error)
            {
                SetState(DccChatState.Disconnected);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            if (!_lifetime.IsCancellationRequested)
            {
                SetState(DccChatState.Error, ex.Message);
            }
        }
        finally
        {
            _client?.Dispose();
        }
    }

    public async Task SendLineAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text.Length > 8192 || text.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("DCC input must be a single line of at most 8192 characters.", nameof(text));
        }

        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _sendGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (_state != DccChatState.Connected || _writer is null)
            {
                throw new InvalidOperationException("DCC chat is not connected.");
            }

            await _writer.WriteLineAsync(text.AsMemory(), linked.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            SetState(DccChatState.Error, ex.Message);
            _lifetime.Cancel();
            _client?.Dispose();
            throw;
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        _lifetime.Cancel();
        _client?.Dispose();
        if (_connectTask is not null)
        {
            await _connectTask.ConfigureAwait(false);
        }

        if (_receiveTask is not null)
        {
            await _receiveTask.ConfigureAwait(false);
        }

        SetState(DccChatState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await DisconnectAsync().ConfigureAwait(false);
        // Let a cancelled write leave its critical section before disposing its writer.
        await _sendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _writer?.Dispose();
        }
        finally
        {
            _lifetime.Dispose();
            _sendGate.Release();
        }
    }

    private void SetState(DccChatState state, string? detail = null)
    {
        _state = state;
        StateChanged?.Invoke(state, detail);
    }
}
