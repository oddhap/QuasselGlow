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
    private static readonly TimeSpan DefaultHeartBeatInterval = TimeSpan.FromSeconds(30);
    private const int DefaultMaxMissedHeartBeats = 2;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly TimeSpan _heartBeatInterval;
    private readonly int _maxMissedHeartBeats;
    private readonly Dictionary<NetworkId, QuasselNetworkState> _networkStates = new();
    private readonly Dictionary<string, QuasselChannelState> _channelStates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] SupportedFeatureList = ["ExtendedFeatures", "LongMessageId"];
    private const uint SupportedLegacyFeatures = 0x8000;
    private const string KnownChannelUserModePriority = "qaohv";

    private CancellationTokenSource? _lifetime;
    private TcpClient? _tcpClient;
    private Stream? _stream;
    private Task? _receiveLoop;
    private Task? _heartBeatLoop;
    private int _missedHeartBeats;
    private bool _useLongMessageIds;

    public event Action<QuasselConnectionState, string?>? ConnectionStateChanged;
    public event Action<QuasselSessionState>? SessionStateReceived;
    public event Action<QuasselNetworkState>? NetworkStateReceived;
    public event Action<QuasselBufferInfo>? BufferInfoUpdated;
    public event Action<QuasselChannelState>? ChannelStateReceived;
    public event Action<QuasselChannelTopicUpdate>? ChannelTopicReceived;
    public event Action<QuasselMessage>? MessageReceived;
    public event Action<BufferId, IReadOnlyList<QuasselMessage>>? BacklogReceived;
    public event Action<string>? StatusReceived;
    public event Action<NetworkId>? NetworkCreated;
    public event Action<NetworkId>? NetworkRemoved;

    public bool IsConnected => _stream is not null;

    public QuasselCoreClient()
        : this(DefaultHeartBeatInterval, DefaultMaxMissedHeartBeats)
    {
    }

    internal QuasselCoreClient(TimeSpan heartBeatInterval, int maxMissedHeartBeats)
    {
        _heartBeatInterval = heartBeatInterval;
        _maxMissedHeartBeats = maxMissedHeartBeats;
    }

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
                        _missedHeartBeats = 0;
                        _receiveLoop = Task.Run(() => ReceiveLoopAsync(token), CancellationToken.None);
                        _heartBeatLoop = Task.Run(() => HeartBeatLoopAsync(token), CancellationToken.None);
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

    public async Task RequestChannelStateAsync(NetworkId networkId, string channelName, CancellationToken cancellationToken = default)
    {
        if (!networkId.IsValid || string.IsNullOrWhiteSpace(channelName))
        {
            return;
        }

        await SendPackedAsync(
            InitRequestType,
            cancellationToken,
            Encoding.UTF8.GetBytes("IrcChannel"),
            Encoding.UTF8.GetBytes($"{networkId.Value}/{channelName.Trim()}")).ConfigureAwait(false);
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

    public async Task RequestBacklogForwardAsync(BufferId bufferId, MsgId first, MsgId last, int amount = 0, CancellationToken cancellationToken = default)
    {
        if (!bufferId.IsValid)
        {
            return;
        }

        await SendPackedAsync(
            SyncRequestType,
            cancellationToken,
            Encoding.UTF8.GetBytes("BacklogManager"),
            Encoding.UTF8.GetBytes(string.Empty),
            Encoding.UTF8.GetBytes("requestBacklogForward"),
            bufferId,
            first,
            last,
            amount,
            0,
            0).ConfigureAwait(false);
    }

    public async Task DeleteBufferAsync(BufferId bufferId, CancellationToken cancellationToken = default)
    {
        if (!bufferId.IsValid)
        {
            return;
        }

        await SendPackedAsync(
            SyncRequestType,
            cancellationToken,
            Encoding.UTF8.GetBytes("BufferSyncer"),
            Encoding.UTF8.GetBytes(string.Empty),
            Encoding.UTF8.GetBytes("requestRemoveBuffer"),
            bufferId).ConfigureAwait(false);
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

    private async Task HeartBeatLoopAsync(CancellationToken cancellationToken)
    {
        if (_heartBeatInterval <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(_heartBeatInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_maxMissedHeartBeats > 0 && Volatile.Read(ref _missedHeartBeats) >= _maxMissedHeartBeats)
                {
                    const string message = "Lost connection to Quassel core: no heartbeat reply received.";
                    StatusReceived?.Invoke(message);
                    await DisconnectInternalAsync(QuasselConnectionState.Error, message, skipReceiveLoopAwait: true, skipHeartBeatLoopAwait: true)
                        .ConfigureAwait(false);
                    return;
                }

                await SendPackedAsync(HeartBeatRequestType, cancellationToken, DateTimeOffset.UtcNow).ConfigureAwait(false);
                Interlocked.Increment(ref _missedHeartBeats);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusReceived?.Invoke(ex.Message);
            await DisconnectInternalAsync(QuasselConnectionState.Error, ex.Message, skipReceiveLoopAwait: true, skipHeartBeatLoopAwait: true).ConfigureAwait(false);
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
            Interlocked.Exchange(ref _missedHeartBeats, 0);
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

        if (className == "BacklogManager" && slotName == "receiveBacklogForward" && parameters.Count >= 7)
        {
            var bufferId = QtValueHelpers.AsBufferId(parameters[0]);
            var messages = QtValueHelpers.AsList(parameters[6]).Select(QtValueHelpers.AsMessage).ToList();
            BacklogReceived?.Invoke(bufferId, messages);
            foreach (var message in messages)
            {
                MessageReceived?.Invoke(message);
            }
            return;
        }

        if (className == "IrcChannel")
        {
            HandleIrcChannelSyncMessage(objectName, slotName, parameters);
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
                RemoveCachedChannels(networkId);
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
        if (className == "IrcChannel")
        {
            HandleIrcChannelInitData(objectName, values);
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

    private void HandleIrcChannelSyncMessage(string objectName, string slotName, IReadOnlyList<object?> parameters)
    {
        if (!TryParseChannelObjectName(objectName, out var networkId, out var channelName))
        {
            return;
        }

        switch (slotName)
        {
            case "setTopic" when parameters.Count > 0:
                UpdateChannelTopic(networkId, channelName, QtValueHelpers.AsString(parameters[0]));
                return;

            case "update" when parameters.Count > 0:
                ApplyChannelUpdate(networkId, channelName, QtValueHelpers.AsMap(parameters[0]));
                return;

            case "joinIrcUsers" when parameters.Count >= 2:
                AddOrUpdateChannelUsers(
                    networkId,
                    channelName,
                    QtValueHelpers.AsStringList(parameters[0]),
                    QtValueHelpers.AsStringList(parameters[1]));
                return;

            case "part" when parameters.Count > 0:
                RemoveChannelUser(networkId, channelName, QtValueHelpers.AsString(parameters[0]));
                return;

            case "setUserModes" when parameters.Count >= 2:
                SetChannelUserModes(
                    networkId,
                    channelName,
                    QtValueHelpers.AsString(parameters[0]),
                    QtValueHelpers.AsString(parameters[1]));
                return;

            case "addUserMode" when parameters.Count >= 2:
                AddChannelUserMode(
                    networkId,
                    channelName,
                    QtValueHelpers.AsString(parameters[0]),
                    QtValueHelpers.AsString(parameters[1]));
                return;

            case "removeUserMode" when parameters.Count >= 2:
                RemoveChannelUserMode(
                    networkId,
                    channelName,
                    QtValueHelpers.AsString(parameters[0]),
                    QtValueHelpers.AsString(parameters[1]));
                return;
        }
    }

    private void HandleIrcChannelInitData(string objectName, IReadOnlyList<object?> values)
    {
        if (!TryParseChannelObjectName(objectName, out var networkId, out var channelName))
        {
            return;
        }

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 2; index + 1 < values.Count; index += 2)
        {
            properties[QtValueHelpers.AsUtf8String(values[index])] = values[index + 1];
        }

        var topic = QtValueHelpers.AsString(properties.GetValueOrDefault("topic"));
        var users = ParseChannelUsers(properties.TryGetValue("UserModes", out var userModes)
            ? userModes
            : properties.GetValueOrDefault("userModes"));

        UpdateChannelState(new QuasselChannelState(networkId, channelName, topic, users));
    }

    private static string? ReadTopicFromVariantMap(object? value)
    {
        var map = QtValueHelpers.AsMap(value);
        return map.TryGetValue("topic", out var topicValue)
            ? QtValueHelpers.AsString(topicValue)
            : null;
    }

    private void ApplyChannelUpdate(NetworkId networkId, string channelName, IReadOnlyDictionary<string, object?> properties)
    {
        var state = GetOrCreateChannelState(networkId, channelName);
        var topic = properties.TryGetValue("topic", out var topicValue)
            ? QtValueHelpers.AsString(topicValue)
            : state.Topic;

        var users = properties.TryGetValue("UserModes", out var userModesValue)
            ? ParseChannelUsers(userModesValue)
            : properties.TryGetValue("userModes", out userModesValue)
                ? ParseChannelUsers(userModesValue)
                : state.Users;

        UpdateChannelState(state with { Topic = topic, Users = users });
    }

    private void UpdateChannelTopic(NetworkId networkId, string channelName, string topic)
    {
        var state = GetOrCreateChannelState(networkId, channelName);
        UpdateChannelState(state with { Topic = topic });
    }

    private void AddOrUpdateChannelUsers(NetworkId networkId, string channelName, IReadOnlyList<string> nicks, IReadOnlyList<string> modes)
    {
        var state = GetOrCreateChannelState(networkId, channelName);
        var users = state.Users.ToDictionary(user => user.Nick, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < nicks.Count; index++)
        {
            var nick = nicks[index].Trim();
            if (string.IsNullOrWhiteSpace(nick))
            {
                continue;
            }

            var mode = index < modes.Count ? NormalizeChannelUserModes(modes[index]) : string.Empty;
            users[nick] = new QuasselChannelUser(nick, mode);
        }

        UpdateChannelState(state with { Users = users.Values.ToArray() });
    }

    private void RemoveChannelUser(NetworkId networkId, string channelName, string nick)
    {
        if (string.IsNullOrWhiteSpace(nick))
        {
            return;
        }

        var state = GetOrCreateChannelState(networkId, channelName);
        var trimmedNick = nick.Trim();
        var users = state.Users
            .Where(user => !string.Equals(user.Nick, trimmedNick, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        UpdateChannelState(state with { Users = users });
    }

    private void SetChannelUserModes(NetworkId networkId, string channelName, string nick, string modes)
    {
        if (string.IsNullOrWhiteSpace(nick))
        {
            return;
        }

        var state = GetOrCreateChannelState(networkId, channelName);
        UpdateChannelState(state with
        {
            Users = UpsertChannelUser(state.Users, nick.Trim(), NormalizeChannelUserModes(modes))
        });
    }

    private void AddChannelUserMode(NetworkId networkId, string channelName, string nick, string mode)
    {
        if (string.IsNullOrWhiteSpace(nick) || string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        var state = GetOrCreateChannelState(networkId, channelName);
        var trimmedNick = nick.Trim();
        var existingModes = state.Users
            .FirstOrDefault(entry => string.Equals(entry.Nick, trimmedNick, StringComparison.OrdinalIgnoreCase))
            ?.Modes ?? string.Empty;

        UpdateChannelState(state with
        {
            Users = UpsertChannelUser(state.Users, trimmedNick, NormalizeChannelUserModes(existingModes + mode.Trim()))
        });
    }

    private void RemoveChannelUserMode(NetworkId networkId, string channelName, string nick, string mode)
    {
        if (string.IsNullOrWhiteSpace(nick) || string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        var trimmedNick = nick.Trim();
        var removalChars = mode.Trim().ToCharArray();
        var state = GetOrCreateChannelState(networkId, channelName);
        var users = state.Users
            .Select(user => string.Equals(user.Nick, trimmedNick, StringComparison.OrdinalIgnoreCase)
                ? user with
                {
                    Modes = NormalizeChannelUserModes(new string(user.Modes.Where(ch => !removalChars.Contains(ch)).ToArray()))
                }
                : user)
            .ToArray();

        UpdateChannelState(state with { Users = users });
    }

    private void UpdateChannelState(QuasselChannelState state)
    {
        var key = BuildChannelKey(state.NetworkId, state.ChannelName);
        var previousTopic = _channelStates.TryGetValue(key, out var previous)
            ? previous.Topic
            : string.Empty;

        _channelStates[key] = state;
        ChannelStateReceived?.Invoke(state);

        if (!string.Equals(previousTopic, state.Topic, StringComparison.Ordinal))
        {
            ChannelTopicReceived?.Invoke(new QuasselChannelTopicUpdate(state.NetworkId, state.ChannelName, state.Topic));
        }
    }

    private QuasselChannelState GetOrCreateChannelState(NetworkId networkId, string channelName)
    {
        var key = BuildChannelKey(networkId, channelName);
        return _channelStates.TryGetValue(key, out var state)
            ? state
            : new QuasselChannelState(networkId, channelName.Trim(), string.Empty, Array.Empty<QuasselChannelUser>());
    }

    private static IReadOnlyList<QuasselChannelUser> ParseChannelUsers(object? value)
    {
        return QtValueHelpers.AsMap(value)
            .Select(entry => new QuasselChannelUser(entry.Key, NormalizeChannelUserModes(QtValueHelpers.AsString(entry.Value))))
            .ToArray();
    }

    private static IReadOnlyList<QuasselChannelUser> UpsertChannelUser(
        IReadOnlyList<QuasselChannelUser> users,
        string nick,
        string modes)
    {
        var updated = false;
        var nextUsers = users
            .Select(user =>
            {
                if (!string.Equals(user.Nick, nick, StringComparison.OrdinalIgnoreCase))
                {
                    return user;
                }

                updated = true;
                return user with { Nick = nick, Modes = modes };
            })
            .ToList();

        if (!updated)
        {
            nextUsers.Add(new QuasselChannelUser(nick, modes));
        }

        return nextUsers;
    }

    private static string NormalizeChannelUserModes(string modes)
    {
        if (string.IsNullOrWhiteSpace(modes))
        {
            return string.Empty;
        }

        var uniqueModes = modes
            .Where(ch => !char.IsWhiteSpace(ch))
            .Distinct()
            .ToList();

        var orderedKnownModes = uniqueModes
            .Where(ch => KnownChannelUserModePriority.Contains(char.ToLowerInvariant(ch)))
            .OrderBy(ch => KnownChannelUserModePriority.IndexOf(char.ToLowerInvariant(ch)));

        var otherModes = uniqueModes
            .Where(ch => !KnownChannelUserModePriority.Contains(char.ToLowerInvariant(ch)))
            .OrderBy(char.ToLowerInvariant);

        return string.Concat(orderedKnownModes.Concat(otherModes));
    }

    private static bool TryParseChannelObjectName(string objectName, out NetworkId networkId, out string channelName)
    {
        networkId = default;
        channelName = string.Empty;

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        var separatorIndex = objectName.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex >= objectName.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(objectName[..separatorIndex], out var rawNetworkId))
        {
            return false;
        }

        networkId = new NetworkId(rawNetworkId);
        channelName = objectName[(separatorIndex + 1)..];
        return networkId.IsValid && !string.IsNullOrWhiteSpace(channelName);
    }

    private static string BuildChannelKey(NetworkId networkId, string channelName)
    {
        return $"{networkId.Value}/{channelName.Trim()}";
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

    private async Task DisconnectInternalAsync(
        QuasselConnectionState endState,
        string? message,
        bool skipReceiveLoopAwait,
        bool skipHeartBeatLoopAwait = false)
    {
        var receiveLoop = _receiveLoop;
        var heartBeatLoop = _heartBeatLoop;
        _receiveLoop = null;
        _heartBeatLoop = null;

        if (_lifetime is { IsCancellationRequested: false })
        {
            _lifetime.Cancel();
        }

        if (!skipReceiveLoopAwait && receiveLoop is not null)
        {
            try
            {
                await receiveLoop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        if (!skipHeartBeatLoopAwait && heartBeatLoop is not null)
        {
            try
            {
                await heartBeatLoop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        _networkStates.Clear();
        _channelStates.Clear();
        _useLongMessageIds = false;
        _missedHeartBeats = 0;

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

    private void RemoveCachedChannels(NetworkId networkId)
    {
        var keysToRemove = _channelStates.Keys
            .Where(key => key.StartsWith($"{networkId.Value}/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var key in keysToRemove)
        {
            _channelStates.Remove(key);
        }
    }
}
