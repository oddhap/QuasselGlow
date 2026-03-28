using Avalonia.Controls;
using QuasselGlow.ViewModels;
using QuasselGlow.Appearance;
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

    [Fact]
    public void SupportedThemes_StayInSyncWithThemeCatalog()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        Assert.Equal(AppThemeCatalog.ThemeKeys, viewModel.SupportedThemes.Select(option => option.Key));
        Assert.Equal(AppThemeCatalog.ModeKeys, viewModel.SupportedThemeModes.Select(option => option.Key));
    }

    [Fact]
    public void ToggleUserListPinned_OpensPanePersistsSettingAndSwitchesDisplayMode()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        Assert.False(viewModel.IsControlPanelOpen);
        Assert.False(viewModel.IsUserListPinned);
        Assert.Equal(SplitViewDisplayMode.Overlay, viewModel.UserListDisplayMode);
        Assert.True(viewModel.UseOverlayDismissForUserList);
        Assert.Equal(viewModel.Strings["Pin"], viewModel.UserListPinButtonText);

        viewModel.ToggleUserListPinnedCommand.Execute(null);

        var pinnedSettings = settings.Load();
        Assert.True(viewModel.IsControlPanelOpen);
        Assert.True(viewModel.IsUserListPinned);
        Assert.Equal(SplitViewDisplayMode.Inline, viewModel.UserListDisplayMode);
        Assert.False(viewModel.UseOverlayDismissForUserList);
        Assert.Equal(viewModel.Strings["Unpin"], viewModel.UserListPinButtonText);
        Assert.True(pinnedSettings.IsControlPanelOpen);
        Assert.True(pinnedSettings.IsUserListPinned);

        viewModel.ToggleUserListPinnedCommand.Execute(null);

        var unpinnedSettings = settings.Load();
        Assert.False(viewModel.IsUserListPinned);
        Assert.Equal(SplitViewDisplayMode.Overlay, viewModel.UserListDisplayMode);
        Assert.True(viewModel.UseOverlayDismissForUserList);
        Assert.Equal(viewModel.Strings["Pin"], viewModel.UserListPinButtonText);
        Assert.True(unpinnedSettings.IsControlPanelOpen);
        Assert.False(unpinnedSettings.IsUserListPinned);
    }

    [Fact]
    public void ChannelStateReceived_PopulatesSortedChannelUsers()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(5), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("zoe", ""),
                new QuasselChannelUser("bob", "v"),
                new QuasselChannelUser("alice", "o")
            ]));

        Assert.Equal("Current topic", viewModel.SelectedBufferSubtitleText);
        Assert.True(viewModel.ShowSelectedChannelUsers);
        Assert.Equal(["alice", "bob", "zoe"], viewModel.SelectedChannelUsers.Select(user => user.Nick));
        Assert.Equal("@", viewModel.SelectedChannelUsers[0].Prefix);
        Assert.Equal("+", viewModel.SelectedChannelUsers[1].Prefix);
    }

    [Fact]
    public async Task GiveOpToChannelUser_SendsModeCommandToSelectedChannel()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(6), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));

        await viewModel.GiveOpToChannelUserCommand.ExecuteAsync(new ChannelUserViewModel(new QuasselChannelUser("bob", string.Empty)));

        Assert.Single(session.SentInputs);
        Assert.Equal(channelBuffer.BufferId, session.SentInputs[0].bufferInfo.BufferId);
        Assert.Equal("/mode #quassel +o bob", session.SentInputs[0].text);
    }

    [Fact]
    public async Task KickBanChannelUser_SendsBanThenKickCommands()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(7), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));

        await viewModel.KickBanChannelUserCommand.ExecuteAsync(new ChannelUserViewModel(new QuasselChannelUser("zoe", string.Empty)));

        Assert.Equal(2, session.SentInputs.Count);
        Assert.Equal("/mode #quassel +b zoe!*@*", session.SentInputs[0].text);
        Assert.Equal("/kick #quassel zoe", session.SentInputs[1].text);
    }

    [Fact]
    public async Task LeaveChannelBuffer_SendsPartCommandToClickedBuffer()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var firstBuffer = new QuasselBufferInfo(new BufferId(8), new NetworkId(1), QuasselBufferType.Channel, 0, "#alpha");
        var secondBuffer = new QuasselBufferInfo(new BufferId(9), new NetworkId(1), QuasselBufferType.Channel, 0, "#beta");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [firstBuffer, secondBuffer], [new NetworkId(1)]));
        var targetBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "#beta");

        await viewModel.LeaveChannelBufferCommand.ExecuteAsync(targetBuffer);

        Assert.Single(session.SentInputs);
        Assert.Equal(secondBuffer.BufferId, session.SentInputs[0].bufferInfo.BufferId);
        Assert.Equal("/part #beta", session.SentInputs[0].text);
        Assert.Contains(viewModel.Networks.Single().Buffers, buffer => buffer.DisplayName == "#beta");
    }

    [Fact]
    public void SelfPartMessage_RemovesChannelBufferFromList()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(14), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var queryBuffer = new QuasselBufferInfo(new BufferId(15), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer, queryBuffer], [new NetworkId(1)]));

        session.EmitMessage(new QuasselMessage(
            new MsgId(21),
            DateTimeOffset.Parse("2026-03-28T13:31:00+01:00"),
            channelBuffer,
            QuasselMessageType.Part,
            "Oddi has left #quassel",
            "Oddi!user@example",
            QuasselMessageFlags.Self));

        Assert.DoesNotContain(viewModel.Networks.Single().Buffers, buffer => buffer.DisplayName == "#quassel");
        Assert.Equal("bob", viewModel.SelectedBuffer?.DisplayName);
    }

    [Fact]
    public async Task WhoisBuffer_SendsWhoisCommandToClickedQueryBuffer()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(10), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var queryBuffer = new QuasselBufferInfo(new BufferId(11), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer, queryBuffer], [new NetworkId(1)]));
        var targetBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "bob");

        await viewModel.WhoisBufferCommand.ExecuteAsync(targetBuffer);

        Assert.Single(session.SentInputs);
        Assert.Equal(queryBuffer.BufferId, session.SentInputs[0].bufferInfo.BufferId);
        Assert.Equal("/whois bob", session.SentInputs[0].text);
    }

    [Fact]
    public void MarkBufferAsRead_ClearsUnreadStateOnClickedBuffer()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(12), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var queryBuffer = new QuasselBufferInfo(new BufferId(13), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer, queryBuffer], [new NetworkId(1)]));
        session.EmitMessage(new QuasselMessage(
            new MsgId(20),
            DateTimeOffset.Parse("2026-03-28T13:30:00+01:00"),
            queryBuffer,
            QuasselMessageType.Plain,
            "ping",
            "bob!user@example",
            QuasselMessageFlags.None));
        var targetBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "bob");

        Assert.Equal(1, targetBuffer.UnreadCount);
        Assert.True(targetBuffer.HasPrivateMessageAlert);

        viewModel.MarkBufferAsReadCommand.Execute(targetBuffer);

        Assert.Equal(0, targetBuffer.UnreadCount);
        Assert.False(targetBuffer.HasPrivateMessageAlert);
        Assert.False(targetBuffer.HasPriorityAlert);
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
        public event Action<QuasselChannelState>? ChannelStateReceived;
        public event Action<QuasselChannelTopicUpdate>? ChannelTopicReceived;
        public event Action<QuasselMessage>? MessageReceived;
        public event Action<string>? StatusReceived;
        public event Action<NetworkId>? NetworkRemoved;

        public List<(QuasselBufferInfo bufferInfo, int amount)> BacklogRequests { get; } = [];
        public List<(QuasselBufferInfo bufferInfo, string text)> SentInputs { get; } = [];

        public Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendInputAsync(QuasselBufferInfo bufferInfo, string text, CancellationToken cancellationToken = default)
        {
            SentInputs.Add((bufferInfo, text));
            return Task.CompletedTask;
        }

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
        public void EmitChannelState(QuasselChannelState state) => ChannelStateReceived?.Invoke(state);
        public void EmitChannelTopic(QuasselChannelTopicUpdate topic) => ChannelTopicReceived?.Invoke(topic);
        public void EmitMessage(QuasselMessage message) => MessageReceived?.Invoke(message);
        public void EmitNetworkRemoved(NetworkId networkId) => NetworkRemoved?.Invoke(networkId);
    }
}
