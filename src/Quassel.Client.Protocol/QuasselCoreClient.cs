using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Quassel.Client.Domain;
using Quassel.Client.Protocol.Qt;

namespace Quassel.Client.Protocol;

public sealed class QuasselCoreClient : IAsyncDisposable
{
    private const uint ProbeMagic = 0x42b33f00;
    private const byte ProbeEncryption = 0x01;
    private const byte DataStreamProtocol = 0x02;
    private const int MaxFrameSize = 64 * 1024 * 1024;
    private const short SyncRequestType = 1;
    private const short RpcRequestType = 2;
    private const short InitRequestType = 3;
    private const short InitDataRequestType = 4;
    private const short HeartBeatRequestType = 5;
    private const short HeartBeatReplyRequestType = 6;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Dictionary<NetworkId, QuasselNetworkState> _networkStates = new();
    private static readonly string[] SupportedFeatureList = ["ExtendedFeatures", "LongMessageId"];
    private const uint SupportedLegacyFeatures = 0x8000;

    private CancellationTokenSource? _lifetime;
    private TcpClient? _tcpClient;
    private Stream? _stream;
    private Task? _receiveLoop;
    private bool _useLongMessageIds;

    public event Action<QuasselConnectionState, string?>? ConnectionStateChanged;
    public event Action<QuasselSessionState>? SessionStateReceived;
    public event Action<QuasselNetworkState>? NetworkStateReceived;
    public event Action<QuasselBufferInfo>? BufferInfoUpdated;
    public event Action<QuasselMessage>? MessageReceived;
    public event Action<BufferId, IReadOnlyList<QuasselMessage>>? BacklogReceived;
    public event Action<string>? StatusReceived;
    public event Action<NetworkId>? NetworkCreated;
    public event Action<NetworkId>? NetworkRemoved;

    public bool IsConnected => _stream is not null;

    public async Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _lifetime.Token;

        try
        {
            OnConnectionStateChanged(QuasselConnectionState.Connecting, $"Connecting to {profile.Host}:{profile.Port}");

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(profile.Host, profile.Port, token).ConfigureAwait(false);

            var networkStream = _tcpClient.GetStream();
            OnConnectionStateChanged(QuasselConnectionState.Negotiating, "Negotiating Quassel protocol");
            var connectionFeatures = await ProbeAsync(networkStream, token).ConfigureAwait(false);

            _stream = networkStream;
            if ((connectionFeatures & ProbeEncryption) != 0)
            {
                OnConnectionStateChanged(QuasselConnectionState.Encrypting, "Establishing TLS");
                _stream = await UpgradeToTlsAsync(networkStream, profile, token).ConfigureAwait(false);
            }

            OnConnectionStateChanged(QuasselConnectionState.Registering, "Registering client");
            await SendHandshakeAsync(BuildRegisterClientHandshake(), token).ConfigureAwait(false);

            var registered = false;
            var loggedIn = false;

            while (!token.IsCancellationRequested)
            {
                var payload = await ReadFrameAsync(token).ConfigureAwait(false);
                var handshake = QtPayloadBuilder.ReadHandshakeMap(payload);
                var messageType = QtValueHelpers.AsString(handshake.GetValueOrDefault("MsgType"));

                switch (messageType)
                {
                    case "ClientInitAck":
                        _useLongMessageIds = SupportsFeature(handshake, "LongMessageId");
                        registered = true;
                        if (!QtValueHelpers.AsBool(handshake.GetValueOrDefault("Configured")))
                        {
                            throw new InvalidOperationException("The connected Quassel core is not configured yet.");
                        }

                        OnConnectionStateChanged(QuasselConnectionState.Authenticating, "Logging in");
                        await SendHandshakeAsync(BuildLoginHandshake(profile), token).ConfigureAwait(false);
                        break;

                    case "ClientInitReject":
                        throw new InvalidOperationException(QtValueHelpers.AsString(handshake.GetValueOrDefault("Error")));

                    case "ClientLoginReject":
                        throw new UnauthorizedAccessException(QtValueHelpers.AsString(handshake.GetValueOrDefault("Error")));

                    case "ClientLoginAck":
                        loggedIn = true;
                        break;

                    case "SessionInit":
                        if (!registered || !loggedIn)
                        {
                            throw new InvalidOperationException("Session state arrived before registration completed.");
                        }

                        var sessionState = ParseSessionState(handshake);
                        OnConnectionStateChanged(QuasselConnectionState.Synchronizing, "Receiving session state");
                        SessionStateReceived?.Invoke(sessionState);
                        _receiveLoop = Task.Run(() => ReceiveLoopAsync(token), CancellationToken.None);
                        OnConnectionStateChanged(QuasselConnectionState.Ready, "Connected");
                        return;

                    default:
                        throw new InvalidOperationException($"Unsupported handshake message: {messageType}");
                }
            }
        }
        catch (Exception ex)
        {
            await DisconnectInternalAsync(QuasselConnectionState.Error, ex.Message, skipReceiveLoopAwait: true).ConfigureAwait(false);
            throw;
        }
    }

    public async Task RequestNetworkStateAsync(NetworkId networkId, CancellationToken cancellationToken = default)
    {
        await SendPackedAsync(
            InitRequestType,
            cancellationToken,
            Encoding.UTF8.GetBytes("Network"),
            Encoding.UTF8.GetBytes(networkId.Value.ToString())).ConfigureAwait(false);
    }

    public async Task RequestBacklogAsync(BufferId bufferId, int amount = 120, CancellationToken cancellationToken = default)
    {
        if (!bufferId.IsValid || amount <= 0)
        {
            return;
        }

        await SendPackedAsync(
            SyncRequestType,
            cancellationToken,
            Encoding.UTF8.GetBytes("BacklogManager"),
            Encoding.UTF8.GetBytes(string.Empty),
            Encoding.UTF8.GetBytes("requestBacklog"),
            bufferId,
            new MsgId(-1),
            new MsgId(-1),
            amount,
            0).ConfigureAwait(false);
    }

    public async Task SendInputAsync(QuasselBufferInfo bufferInfo, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || !bufferInfo.BufferId.IsValid)
        {
            return;
        }

        await SendPackedAsync(
            RpcRequestType,
            cancellationToken,
            Encoding.UTF8.GetBytes("2sendInput(BufferInfo,QString)"),
            bufferInfo,
            text).ConfigureAwait(false);
    }

    public Task DisconnectAsync()
    {
        return DisconnectInternalAsync(QuasselConnectionState.Disconnected, "Disconnected", skipReceiveLoopAwait: false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
                await HandlePackedMessageAsync(payload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusReceived?.Invoke(ex.Message);
            await DisconnectInternalAsync(QuasselConnectionState.Error, ex.Message, skipReceiveLoopAwait: true).ConfigureAwait(false);
        }
    }

    private async Task HandlePackedMessageAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var reader = new QtBinaryReader(payload, _useLongMessageIds);
        var itemCount = reader.ReadUInt32();
        if (itemCount == 0)
        {
            return;
        }

        var requestType = QtValueHelpers.AsInt(reader.ReadVariant());
        if (requestType == HeartBeatRequestType && itemCount == 2)
        {
            await SendRawPackedAsync(HeartBeatReplyRequestType, reader.RemainingMemory(), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (requestType == HeartBeatReplyRequestType)
        {
            return;
        }

        var values = new List<object?>((int)itemCount - 1);
        for (var index = 1; index < itemCount; index++)
        {
            values.Add(reader.ReadVariant());
        }

        switch (requestType)
        {
            case SyncRequestType:
                HandleSyncMessage(values);
                break;
            case RpcRequestType:
                await HandleRpcMessageAsync(values, cancellationToken).ConfigureAwait(false);
                break;
            case InitDataRequestType:
                HandleInitData(values);
                break;
        }
    }

    private void HandleSyncMessage(List<object?> values)
    {
        if (values.Count < 3)
        {
            return;
        }

        var className = QtValueHelpers.AsUtf8String(values[0]);
        var objectName = QtValueHelpers.AsUtf8String(values[1]);
        var slotName = QtValueHelpers.AsUtf8String(values[2]);
        var parameters = values.Skip(3).ToList();

        if (className == "BacklogManager" && slotName == "receiveBacklog" && parameters.Count >= 6)
        {
            var bufferId = QtValueHelpers.AsBufferId(parameters[0]);
            var messages = QtValueHelpers.AsList(parameters[5]).Select(QtValueHelpers.AsMessage).ToList();
            BacklogReceived?.Invoke(bufferId, messages);
            foreach (var message in messages)
            {
                MessageReceived?.Invoke(message);
            }
            return;
        }

        if (className != "Network")
        {
            return;
        }

        var networkId = new NetworkId(int.TryParse(objectName, out var rawId) ? rawId : 0);
        if (!networkId.IsValid)
        {
            return;
        }

        var state = _networkStates.TryGetValue(networkId, out var existing)
            ? existing
            : new QuasselNetworkState(networkId, $"Network {networkId.Value}", string.Empty, string.Empty, false, 0, 0);

        state = slotName switch
        {
            "setNetworkName" when parameters.Count > 0 => state with { NetworkName = QtValueHelpers.AsString(parameters[0]) },
            "setCurrentServer" when parameters.Count > 0 => state with { CurrentServer = QtValueHelpers.AsString(parameters[0]) },
            "setMyNick" when parameters.Count > 0 => state with { MyNick = QtValueHelpers.AsString(parameters[0]) },
            "setConnected" when parameters.Count > 0 => state with { IsConnected = QtValueHelpers.AsBool(parameters[0]) },
            "setLatency" when parameters.Count > 0 => state with { Latency = QtValueHelpers.AsInt(parameters[0]) },
            "setConnectionState" when parameters.Count > 0 => state with { ConnectionState = QtValueHelpers.AsInt(parameters[0]) },
            _ => state,
        };

        _networkStates[networkId] = state;
        NetworkStateReceived?.Invoke(state);
    }

    private async Task HandleRpcMessageAsync(List<object?> values, CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return;
        }

        var signalName = QtValueHelpers.AsUtf8String(values[0]);
        var parameters = values.Skip(1).ToList();

        switch (signalName)
        {
            case "2displayMsg(Message)" when parameters.Count > 0:
                MessageReceived?.Invoke(QtValueHelpers.AsMessage(parameters[0]));
                break;

            case "2displayStatusMsg(QString,QString)" when parameters.Count >= 2:
                StatusReceived?.Invoke($"{QtValueHelpers.AsString(parameters[0])}: {QtValueHelpers.AsString(parameters[1])}");
                break;

            case "2bufferInfoUpdated(BufferInfo)" when parameters.Count > 0:
                BufferInfoUpdated?.Invoke(QtValueHelpers.AsBufferInfo(parameters[0]));
                break;

            case "2networkCreated(NetworkId)" when parameters.Count > 0:
            {
                var networkId = QtValueHelpers.AsNetworkId(parameters[0]);
                NetworkCreated?.Invoke(networkId);
                await RequestNetworkStateAsync(networkId, cancellationToken).ConfigureAwait(false);
                break;
            }

            case "2networkRemoved(NetworkId)" when parameters.Count > 0:
            {
                var networkId = QtValueHelpers.AsNetworkId(parameters[0]);
                _networkStates.Remove(networkId);
                NetworkRemoved?.Invoke(networkId);
                break;
            }

            case "2disconnectFromCore()":
                await DisconnectInternalAsync(QuasselConnectionState.Disconnected, "Disconnected by core", skipReceiveLoopAwait: true)
                    .ConfigureAwait(false);
                break;
        }
    }

    private void HandleInitData(List<object?> values)
    {
        if (values.Count < 3)
        {
            return;
        }

        var className = QtValueHelpers.AsUtf8String(values[0]);
        var objectName = QtValueHelpers.AsUtf8String(values[1]);
        if (className != "Network")
        {
            return;
        }

        var networkId = new NetworkId(int.TryParse(objectName, out var rawId) ? rawId : 0);
        if (!networkId.IsValid)
        {
            return;
        }

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 2; index + 1 < values.Count; index += 2)
        {
            properties[QtValueHelpers.AsUtf8String(values[index])] = values[index + 1];
        }

        var state = new QuasselNetworkState(
            networkId,
            QtValueHelpers.AsString(properties.GetValueOrDefault("networkName")),
            QtValueHelpers.AsString(properties.GetValueOrDefault("currentServer")),
            QtValueHelpers.AsString(properties.GetValueOrDefault("myNick")),
            QtValueHelpers.AsBool(properties.GetValueOrDefault("isConnected")),
            QtValueHelpers.AsInt(properties.GetValueOrDefault("latency")),
            QtValueHelpers.AsInt(properties.GetValueOrDefault("connectionState")));

        _networkStates[networkId] = state;
        NetworkStateReceived?.Invoke(state);
    }

    private QuasselSessionState ParseSessionState(Dictionary<string, object?> handshake)
    {
        var map = QtValueHelpers.AsMap(handshake.GetValueOrDefault("SessionState"));
        var identities = QtValueHelpers.AsList(map.GetValueOrDefault("Identities"));
        var buffers = QtValueHelpers.AsList(map.GetValueOrDefault("BufferInfos"))
            .Select(QtValueHelpers.AsBufferInfo)
            .ToList();
        var networks = QtValueHelpers.AsList(map.GetValueOrDefault("NetworkIds"))
            .Select(QtValueHelpers.AsNetworkId)
            .ToList();

        return new QuasselSessionState(identities, buffers, networks);
    }

    private static IReadOnlyList<object?> BuildRegisterClientHandshake()
    {
        return
        [
            Encoding.UTF8.GetBytes("MsgType"),
            "ClientInit",
            Encoding.UTF8.GetBytes("Features"),
            SupportedLegacyFeatures,
            Encoding.UTF8.GetBytes("FeatureList"),
            SupportedFeatureList,
            Encoding.UTF8.GetBytes("ClientVersion"),
            "Quassel Glow",
            Encoding.UTF8.GetBytes("ClientDate"),
            DateTime.UtcNow.ToString("yyyy-MM-dd")
        ];
    }

    private static IReadOnlyList<object?> BuildLoginHandshake(ConnectionProfile profile)
    {
        return
        [
            Encoding.UTF8.GetBytes("MsgType"),
            "ClientLogin",
            Encoding.UTF8.GetBytes("User"),
            profile.Username,
            Encoding.UTF8.GetBytes("Password"),
            profile.Password
        ];
    }

    private async Task<byte> ProbeAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var probe = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(probe.AsSpan(0, 4), ProbeMagic | ProbeEncryption);
        BinaryPrimitives.WriteUInt32BigEndian(probe.AsSpan(4, 4), 0x80000002);
        await stream.WriteAsync(probe, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var replyBuffer = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        var reply = BinaryPrimitives.ReadUInt32BigEndian(replyBuffer);
        var protocol = (byte)(reply & 0xff);
        if (protocol != DataStreamProtocol)
        {
            throw new InvalidOperationException("The connected Quassel core does not support the DataStream protocol.");
        }

        return (byte)((reply >> 24) & 0xff);
    }

    private static async Task<SslStream> UpgradeToTlsAsync(Stream innerStream, ConnectionProfile profile, CancellationToken cancellationToken)
    {
        var sslStream = new SslStream(
            innerStream,
            false,
            (_, _, _, errors) => errors == SslPolicyErrors.None || profile.TrustInvalidCertificates);

        await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = profile.Host,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.None,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                },
                cancellationToken)
            .ConfigureAwait(false);

        return sslStream;
    }

    private async Task<ReadOnlyMemory<byte>> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("No active Quassel stream.");
        }

        var header = await ReadExactAsync(_stream, 4, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length == 0 || length > MaxFrameSize)
        {
            throw new InvalidDataException($"Received invalid Quassel frame size: {length}.");
        }

        return await ReadExactAsync(_stream, (int)length, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendHandshakeAsync(IReadOnlyList<object?> values, CancellationToken cancellationToken)
    {
        var payload = QtPayloadBuilder.BuildHandshakeList(values);
        await SendFrameAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendPackedAsync(short requestType, CancellationToken cancellationToken, params object?[] values)
    {
        var payload = QtPayloadBuilder.BuildPackedMessage(requestType, _useLongMessageIds, values);
        await SendFrameAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendRawPackedAsync(short requestType, ReadOnlyMemory<byte> rawVariant, CancellationToken cancellationToken)
    {
        var payload = QtPayloadBuilder.BuildPackedMessageWithRawVariant(requestType, rawVariant.Span, _useLongMessageIds);
        await SendFrameAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendFrameAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("No active Quassel stream.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var header = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The Quassel core closed the connection unexpectedly.");
            }

            offset += read;
        }

        return buffer;
    }

    private async Task DisconnectInternalAsync(QuasselConnectionState endState, string? message, bool skipReceiveLoopAwait)
    {
        var loop = _receiveLoop;
        _receiveLoop = null;

        if (_lifetime is { IsCancellationRequested: false })
        {
            _lifetime.Cancel();
        }

        if (!skipReceiveLoopAwait && loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        _networkStates.Clear();
        _useLongMessageIds = false;

        if (_stream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _stream?.Dispose();
        }

        _stream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        _lifetime?.Dispose();
        _lifetime = null;

        OnConnectionStateChanged(endState, message);
    }

    private void OnConnectionStateChanged(QuasselConnectionState state, string? message)
    {
        ConnectionStateChanged?.Invoke(state, message);
    }

    private static bool SupportsFeature(IReadOnlyDictionary<string, object?> handshake, string featureName)
    {
        var featureList = QtValueHelpers.AsStringList(handshake.GetValueOrDefault("FeatureList"));
        return featureList.Contains(featureName, StringComparer.Ordinal);
    }
}
