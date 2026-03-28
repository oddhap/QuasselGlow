using QuasselGlow.ViewModels;
using Quassel.Client.Domain;
using Quassel.Client.Infrastructure;

namespace Quassel.Client.Application.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void StatusReceived_PreservesPrimaryConnectionStateAndUsesDetailText()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitStatus("Core: backlog ready");

        Assert.Equal(viewModel.Strings["StatusConnected"], viewModel.StatusText);
        Assert.Equal("Core: backlog ready", viewModel.ConnectionStatusDetailText);
    }

    [Fact]
    public void SessionStateReceived_SelectsInitialBufferAndUpdatesSessionSummary()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var queryBuffer = new QuasselBufferInfo(new BufferId(2), new NetworkId(1), QuasselBufferType.Query, 0, "alice");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer, queryBuffer], [new NetworkId(1)]));

        Assert.Equal("#quassel", viewModel.SelectedBuffer?.DisplayName);
        Assert.Equal(viewModel.Strings.Format("SessionSummaryFormat", 1, 2), viewModel.SessionSummaryText);
        Assert.Single(session.BacklogRequests);
        Assert.Equal(channelBuffer.BufferId, session.BacklogRequests[0].bufferInfo.BufferId);
    }

    [Fact]
    public void ErrorState_ClearsBuffersAndReopensConnectionEditor()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitConnectionState(QuasselConnectionState.Error, "Login failed");

        Assert.Equal("Login failed", viewModel.StatusText);
        Assert.True(viewModel.IsConnectionEditorOpen);
        Assert.Null(viewModel.SelectedBuffer);
        Assert.Empty(viewModel.Networks);
        Assert.True(viewModel.ShowConnectAction);
        Assert.False(viewModel.ShowDisconnectAction);
    }

    [Fact]
    public void ChannelTopicMessage_UpdatesSelectedBufferSubtitle()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitMessage(new QuasselMessage(
            new MsgId(10),
            DateTimeOffset.Parse("2026-03-28T13:30:00+01:00"),
            channelBuffer,
            QuasselMessageType.Topic,
            "Oddi has changed topic for #quassel to: \"Fresh topic\"",
            "alice!user@example",
            QuasselMessageFlags.None));

        Assert.Equal("Fresh topic", viewModel.SelectedBufferSubtitleText);
        Assert.True(viewModel.ShowSelectedBufferSubtitle);
    }

    [Fact]
    public void QueryBuffer_HidesSelectedBufferSubtitle()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var queryBuffer = new QuasselBufferInfo(new BufferId(2), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [queryBuffer], [new NetworkId(1)]));

        Assert.Equal(string.Empty, viewModel.SelectedBufferSubtitleText);
        Assert.False(viewModel.ShowSelectedBufferSubtitle);
    }

    [Fact]
    public void ChannelTopicUpdate_UsesTopicForSelectedBufferSubtitle()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(3), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelTopic(new QuasselChannelTopicUpdate(new NetworkId(1), "#quassel", "Current channel topic"));

        Assert.Equal("Current channel topic", viewModel.SelectedBufferSubtitleText);
        Assert.True(viewModel.ShowSelectedBufferSubtitle);
    }

    [Fact]
    public void ChannelWithoutTopic_DoesNotShowGenericFallbackSubtitle()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(4), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));

        Assert.Equal(string.Empty, viewModel.SelectedBufferSubtitleText);
        Assert.False(viewModel.ShowSelectedBufferSubtitle);
    }

    private sealed class FakeSettingsStore(StoredConnectionSettings settings) : IConnectionSettingsStore
    {
        private StoredConnectionSettings _settings = settings;

        public StoredConnectionSettings Load() => _settings;

        public void Save(StoredConnectionSettings settings)
        {
            _settings = settings;
        }
    }

    private sealed class FakeSessionService : IQuasselSessionService
    {
        public event Action<QuasselConnectionState, string?>? ConnectionStateChanged;
        public event Action<QuasselSessionState>? SessionStateReceived;
        public event Action<QuasselNetworkState>? NetworkStateReceived;
        public event Action<QuasselBufferInfo>? BufferInfoUpdated;
        public event Action<QuasselChannelTopicUpdate>? ChannelTopicReceived;
        public event Action<QuasselMessage>? MessageReceived;
        public event Action<string>? StatusReceived;
        public event Action<NetworkId>? NetworkRemoved;

        public List<(QuasselBufferInfo bufferInfo, int amount)> BacklogRequests { get; } = [];

        public Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendInputAsync(QuasselBufferInfo bufferInfo, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureBacklogAsync(QuasselBufferInfo bufferInfo, int amount = 120, CancellationToken cancellationToken = default)
        {
            BacklogRequests.Add((bufferInfo, amount));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void EmitConnectionState(QuasselConnectionState state, string? message) => ConnectionStateChanged?.Invoke(state, message);
        public void EmitSessionState(QuasselSessionState state) => SessionStateReceived?.Invoke(state);
        public void EmitStatus(string message) => StatusReceived?.Invoke(message);
        public void EmitNetworkState(QuasselNetworkState state) => NetworkStateReceived?.Invoke(state);
        public void EmitBufferInfo(QuasselBufferInfo bufferInfo) => BufferInfoUpdated?.Invoke(bufferInfo);
        public void EmitChannelTopic(QuasselChannelTopicUpdate topic) => ChannelTopicReceived?.Invoke(topic);
        public void EmitMessage(QuasselMessage message) => MessageReceived?.Invoke(message);
        public void EmitNetworkRemoved(NetworkId networkId) => NetworkRemoved?.Invoke(networkId);
    }
}
