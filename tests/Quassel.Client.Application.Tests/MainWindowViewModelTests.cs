using Avalonia.Controls;
using Avalonia.Media;
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
    public void SettingsLoadFailure_UsesStatusDetailUntilSettingsSaveRecovers()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"))
        {
            LoadResult = new ConnectionSettingsLoadResult(
                new StoredConnectionSettings(Host: "chat.example", Username: "alice"),
                ConnectionSettingsLoadStatus.Failed,
                "bad json")
        };
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        Assert.Equal(viewModel.Strings["LocalStateConnectionPreferencesLoadFailed"], viewModel.ConnectionStatusDetailText);

        viewModel.Host = "irc.example";

        Assert.Equal(viewModel.Strings.Format("ConnectionSummaryWithIdentity", viewModel.ConnectionEndpointText, "alice"), viewModel.ConnectionStatusDetailText);
    }

    [Fact]
    public void SettingsSaveFailure_HasPriorityOverCredentialAndMessageCacheFailures()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"))
        {
            NextSaveResult = ConnectionSettingsSaveResult.Failed("disk full")
        };
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        session.EmitMessageCacheOperation(MessageCacheOperationResult.Degraded("cache failed"));
        viewModel.RememberLogin = true;

        Assert.Equal(viewModel.Strings["LocalStateConnectionPreferencesSaveFailed"], viewModel.ConnectionStatusDetailText);
    }

    [Fact]
    public void CredentialProtectionDegraded_ClearsAfterRememberLoginIsDisabledAndSaved()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"))
        {
            NextSaveResult = ConnectionSettingsSaveResult.SavedWithDegradedCredentialProtection()
        };
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.RememberLogin = true;

        Assert.Equal(viewModel.Strings["LocalStateCredentialProtectionDegraded"], viewModel.ConnectionStatusDetailText);

        settings.NextSaveResult = ConnectionSettingsSaveResult.Saved();
        viewModel.RememberLogin = false;

        Assert.Equal(viewModel.Strings.Format("ConnectionSummaryWithIdentity", viewModel.ConnectionEndpointText, "alice"), viewModel.ConnectionStatusDetailText);
    }

    [Fact]
    public void MessageCacheFailure_ClearsAfterNextSuccessfulCacheOperation()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        session.EmitMessageCacheOperation(MessageCacheOperationResult.Degraded("cache failed"));

        Assert.Equal(viewModel.Strings["LocalStateMessageCacheDegraded"], viewModel.ConnectionStatusDetailText);

        session.EmitMessageCacheOperation(MessageCacheOperationResult.Available());

        Assert.Equal(viewModel.Strings.Format("ConnectionSummaryWithIdentity", viewModel.ConnectionEndpointText, "alice"), viewModel.ConnectionStatusDetailText);
    }

    [Fact]
    public void StartupAutoConnect_UsesStoredServerWhenLoginIsRemembered()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(
            Host: "chat.example",
            Port: 4242,
            Username: "alice",
            Password: "hemmelig",
            TrustInvalidCertificates: true,
            RememberLogin: true,
            AutoConnectOnStartup: true));

        _ = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        var profile = Assert.Single(session.ConnectRequests);
        Assert.Equal("chat.example", profile.Host);
        Assert.Equal(4242, profile.Port);
        Assert.Equal("alice", profile.Username);
        Assert.Equal("hemmelig", profile.Password);
        Assert.True(profile.TrustInvalidCertificates);
    }

    [Fact]
    public void DisablingRememberLogin_TurnsOffAutoConnect()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(
            Host: "chat.example",
            Username: "alice",
            Password: "hemmelig",
            RememberLogin: true,
            AutoConnectOnStartup: true));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.RememberLogin = false;

        Assert.False(viewModel.AutoConnectOnStartup);
        Assert.False(settings.Load().Settings.AutoConnectOnStartup);
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
    public async Task SessionStateReceived_LoadsCachedMessagesBeforeRequestingBacklog()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(41), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        session.CachedMessages[channelBuffer.BufferId] =
        [
            new QuasselMessage(
                new MsgId(100),
                DateTimeOffset.Parse("2026-04-08T20:00:00+02:00"),
                channelBuffer,
                QuasselMessageType.Plain,
                "cached line",
                "alice!user@example",
                QuasselMessageFlags.Backlog)
        ];

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        await Task.Delay(25);

        Assert.NotNull(viewModel.SelectedBuffer);
        Assert.Single(viewModel.SelectedBuffer.Messages);
        Assert.Equal("cached line", viewModel.SelectedBuffer.Messages[0].LineText);
        Assert.Single(session.BacklogRequests);
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
    public async Task StatusBuffer_AllowsSendingJoinCommands()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(3), new NetworkId(1), QuasselBufferType.Status, 0, "Status");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        Assert.True(viewModel.SelectedBuffer?.AcceptsInput);

        viewModel.DraftMessage = "/join #quassel";
        await viewModel.SendMessageCommand.ExecuteAsync(null);

        Assert.Single(session.SentInputs);
        Assert.Equal(statusBuffer.BufferId, session.SentInputs[0].bufferInfo.BufferId);
        Assert.Equal("/join #quassel", session.SentInputs[0].text);
    }

    [Fact]
    public async Task ComposerHistory_UpAndDownRestoreBufferedDraft()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(30), new NetworkId(1), QuasselBufferType.Status, 0, "Status");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        viewModel.DraftMessage = "first";
        await viewModel.SendMessageCommand.ExecuteAsync(null);
        viewModel.DraftMessage = "second";
        await viewModel.SendMessageCommand.ExecuteAsync(null);
        viewModel.DraftMessage = "working draft";

        Assert.True(viewModel.TryRecallPreviousDraft());
        Assert.Equal("second", viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallPreviousDraft());
        Assert.Equal("first", viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallNextDraft());
        Assert.Equal("second", viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallNextDraft());
        Assert.Equal("working draft", viewModel.DraftMessage);
    }

    [Fact]
    public async Task ComposerHistory_AndDraftAreScopedPerBuffer()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var firstBuffer = new QuasselBufferInfo(new BufferId(31), new NetworkId(1), QuasselBufferType.Status, 0, "Status");
        var secondBuffer = new QuasselBufferInfo(new BufferId(32), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [firstBuffer, secondBuffer], [new NetworkId(1)]));

        viewModel.SelectedBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.BufferInfo.BufferId == firstBuffer.BufferId);
        viewModel.DraftMessage = "status message";
        await viewModel.SendMessageCommand.ExecuteAsync(null);

        viewModel.SelectedBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.BufferInfo.BufferId == secondBuffer.BufferId);
        viewModel.DraftMessage = "query draft";

        Assert.Equal("query draft", viewModel.DraftMessage);

        viewModel.SelectedBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.BufferInfo.BufferId == firstBuffer.BufferId);

        Assert.Equal(string.Empty, viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallPreviousDraft());
        Assert.Equal("status message", viewModel.DraftMessage);

        viewModel.SelectedBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.BufferInfo.BufferId == secondBuffer.BufferId);

        Assert.Equal("query draft", viewModel.DraftMessage);
    }

    [Fact]
    public async Task ComposerHistory_UpFromActiveDraftShowsHistoryAndDownRestoresDraft()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(33), new NetworkId(1), QuasselBufferType.Status, 0, "Status");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        viewModel.DraftMessage = "sent message";
        await viewModel.SendMessageCommand.ExecuteAsync(null);

        viewModel.DraftMessage = "draft to stash";

        Assert.True(viewModel.TryRecallPreviousDraft());
        Assert.Equal("sent message", viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallNextDraft());
        Assert.Equal("draft to stash", viewModel.DraftMessage);
    }

    [Fact]
    public async Task ComposerHistory_DownAfterSingleUpRestoresDraftImmediately()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(34), new NetworkId(1), QuasselBufferType.Status, 0, "Status");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        viewModel.DraftMessage = "sent message";
        await viewModel.SendMessageCommand.ExecuteAsync(null);
        viewModel.DraftMessage = "draft to restore";

        Assert.True(viewModel.TryRecallPreviousDraft());
        Assert.Equal("sent message", viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallNextDraft());
        Assert.Equal("draft to restore", viewModel.DraftMessage);
    }

    [Fact]
    public async Task ComposerHistory_DownFromLatestHistoryWithoutDraftClearsComposer()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(35), new NetworkId(1), QuasselBufferType.Status, 0, "Status");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        viewModel.DraftMessage = "first";
        await viewModel.SendMessageCommand.ExecuteAsync(null);
        viewModel.DraftMessage = "second";
        await viewModel.SendMessageCommand.ExecuteAsync(null);

        Assert.True(viewModel.TryRecallPreviousDraft());
        Assert.Equal("second", viewModel.DraftMessage);

        Assert.True(viewModel.TryRecallNextDraft());
        Assert.Equal(string.Empty, viewModel.DraftMessage);
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
    public void PlatformAndInspiredThemes_AppearWithDisplayNames()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var expectedThemes = new[]
        {
            ("windows7", "Windows 7"),
            ("windows10", "Windows 10"),
            ("windows11", "Windows 11"),
            ("dynamicWallpaper", "Dynamic Wallpaper"),
            ("ubuntu", "Ubuntu"),
            ("cobalt", "Cobalt"),
            ("slate", "Slate"),
            ("frost", "Frost"),
            ("aubergine", "Aubergine")
        };

        foreach (var (key, displayName) in expectedThemes)
        {
            var option = Assert.Single(viewModel.SupportedThemes, theme => theme.Key == key);
            Assert.Equal(displayName, option.DisplayName);

            viewModel.SelectedThemeKey = key;
            Assert.Equal(displayName, viewModel.SelectedTheme?.DisplayName);
        }

        viewModel.SelectedLanguageCode = "nb";

        foreach (var (key, displayName) in expectedThemes)
        {
            viewModel.SelectedThemeKey = key;
            Assert.Equal(displayName, viewModel.SelectedTheme?.DisplayName);
        }
    }

    [Fact]
    public void DynamicWallpaperTheme_UsesProvidedWallpaperColors()
    {
        var wallpaperColors = new WallpaperThemeColors(Color.Parse("#0E8C86"), Color.Parse("#B95F2D"));

        var dynamicPalette = AppThemeCatalog.ResolvePalette(
            AppThemeCatalog.DynamicWallpaperThemeKey,
            "light",
            wallpaperColors);
        var fallbackPalette = AppThemeCatalog.ResolvePalette(
            AppThemeCatalog.DynamicWallpaperThemeKey,
            "light");
        var glowPalette = AppThemeCatalog.ResolvePalette("glow", "light");

        Assert.NotEqual(glowPalette.AccentTeal, dynamicPalette.AccentTeal);
        Assert.NotEqual(glowPalette.AccentRust, dynamicPalette.AccentRust);
        Assert.Equal(glowPalette.AccentTeal, fallbackPalette.AccentTeal);
    }

    [Fact]
    public void WallpaperPaletteProvider_SelectsDominantReadableColors()
    {
        var colors = Enumerable.Repeat(Color.Parse("#147D78"), 40)
            .Concat(Enumerable.Repeat(Color.Parse("#A85B2A"), 24))
            .Concat(Enumerable.Repeat(Color.Parse("#F8F8F8"), 80));

        var selected = WallpaperPaletteProvider.SelectThemeColors(colors);

        Assert.NotNull(selected);
        Assert.True(selected.Primary.G > selected.Primary.R);
        Assert.True(selected.Secondary.R > selected.Secondary.B);
    }

    [Fact]
    public void ToggleThemeEditor_ClosesConnectionEditorAndUpdatesThemeSummary()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.ToggleConnectionEditorCommand.Execute(null);
        Assert.True(viewModel.IsConnectionEditorOpen);

        viewModel.ToggleThemeEditorCommand.Execute(null);

        Assert.True(viewModel.IsThemeEditorOpen);
        Assert.False(viewModel.IsConnectionEditorOpen);

        viewModel.SelectedTheme = viewModel.SupportedThemes.Last();
        viewModel.SelectedThemeMode = viewModel.SupportedThemeModes.Last();

        Assert.Equal(
            $"{viewModel.SelectedTheme!.DisplayName} / {viewModel.SelectedThemeMode!.DisplayName}",
            viewModel.ThemeSummaryText);
    }

    [Fact]
    public void ConnectionEditor_OnlyShowsForActiveLayout()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.SetCompactLayout(false);
        viewModel.ToggleConnectionEditorCommand.Execute(null);

        Assert.True(viewModel.ShowDesktopConnectionEditor);
        Assert.False(viewModel.ShowCompactConnectionEditor);

        viewModel.SetCompactLayout(true);

        Assert.False(viewModel.ShowDesktopConnectionEditor);
        Assert.True(viewModel.ShowCompactConnectionEditor);
    }

    [Fact]
    public void LowResolutionLayout_HidesInlinePanelsAndUsesOverviewOverlay()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.SetCompactLayout(true);
        viewModel.SetLowResolutionLayout(true);

        Assert.False(viewModel.ShowDesktopTopPanels);
        Assert.False(viewModel.ShowCompactTopPanels);
        Assert.True(viewModel.ShowLowResolutionOverviewButton);
        Assert.False(viewModel.ShowLowResolutionOverview);

        viewModel.ToggleOverviewCommand.Execute(null);

        Assert.True(viewModel.ShowLowResolutionOverview);

        viewModel.CloseOverviewCommand.Execute(null);

        Assert.False(viewModel.ShowLowResolutionOverview);
    }

    [Fact]
    public void ThemeEditor_UsesLowResolutionOverlayWhenNeeded()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.SetCompactLayout(true);
        viewModel.SetLowResolutionLayout(true);

        viewModel.ToggleThemeEditorCommand.Execute(null);

        Assert.True(viewModel.ShowLowResolutionOverview);
        Assert.True(viewModel.ShowLowResolutionThemeEditor);
        Assert.False(viewModel.ShowCompactThemeEditor);
        Assert.False(viewModel.ShowDesktopThemeEditor);

        viewModel.CloseOverviewCommand.Execute(null);

        Assert.False(viewModel.IsThemeEditorOpen);
        Assert.False(viewModel.ShowLowResolutionOverview);
    }

    [Fact]
    public void ChangingLanguage_PreservesLocalizedThemeModeSelection()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.SelectedThemeKey = AppThemeCatalog.ThemeKeys.Last();
        viewModel.SelectedThemeModeKey = "dark";
        viewModel.SelectedLanguageCode = "nb";
        var selectedTheme = viewModel.SelectedTheme;
        var selectedMode = viewModel.SelectedThemeMode;

        viewModel.SelectedLanguageCode = "en_US";

        Assert.Equal(AppThemeCatalog.ThemeKeys.Last(), viewModel.SelectedThemeKey);
        Assert.Equal("dark", viewModel.SelectedThemeModeKey);
        Assert.Same(selectedTheme, viewModel.SelectedTheme);
        Assert.Same(selectedMode, viewModel.SelectedThemeMode);
        Assert.Contains(viewModel.SelectedTheme!, viewModel.SupportedThemes);
        Assert.Contains(viewModel.SelectedThemeMode!, viewModel.SupportedThemeModes);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.SelectedTheme?.DisplayName));
        Assert.Equal(viewModel.Strings["ThemeModeDark"], viewModel.SelectedThemeMode?.DisplayName);
    }

    [Fact]
    public void ChangingLanguage_BetweenLanguagesWithSameThemeModeText_PreservesSelectedThemeMode()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.SelectedThemeModeKey = "dark";
        viewModel.SelectedLanguageCode = "nb";
        var norwegianSelection = viewModel.SelectedThemeMode;

        viewModel.SelectedLanguageCode = "da";
        var danishSelection = viewModel.SelectedThemeMode;

        Assert.NotNull(norwegianSelection);
        Assert.NotNull(danishSelection);
        Assert.Same(norwegianSelection, danishSelection);
        Assert.Equal("dark", danishSelection.Key);
        Assert.Contains(danishSelection, viewModel.SupportedThemeModes);
        Assert.Equal(viewModel.Strings["ThemeModeDark"], danishSelection.DisplayName);
    }

    [Fact]
    public void UserList_IsAlwaysPinnedInDesktopLayout()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        Assert.False(viewModel.IsControlPanelOpen);
        Assert.Equal(SplitViewDisplayMode.Inline, viewModel.UserListDisplayMode);
        Assert.False(viewModel.UseOverlayDismissForUserList);
    }

    [Fact]
    public void UserList_UsesOverlayInCompactLayout()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        viewModel.SetCompactLayout(true);

        Assert.Equal(SplitViewDisplayMode.Overlay, viewModel.UserListDisplayMode);
        Assert.True(viewModel.UseOverlayDismissForUserList);
    }

    [Fact]
    public void UserListToggle_IsHiddenWhileUserListIsOpen()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);

        Assert.True(viewModel.ShowUserListToggle);

        viewModel.ToggleControlPanelCommand.Execute(null);

        Assert.False(viewModel.ShowUserListToggle);

        viewModel.ToggleControlPanelCommand.Execute(null);

        Assert.True(viewModel.ShowUserListToggle);
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
    public void NickAutocomplete_CompletesFirstChannelNickWithColon()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(50), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("alice", "o"),
                new QuasselChannelUser("bob", string.Empty)
            ]));

        viewModel.DraftMessage = "bo";

        var handled = viewModel.TryAutocompleteNick(2, out var caretIndex);

        Assert.True(handled);
        Assert.Equal("bob: ", viewModel.DraftMessage);
        Assert.Equal(5, caretIndex);
    }

    [Fact]
    public void NickAutocomplete_CompletesInlineNickWithoutColon()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(51), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("bob", string.Empty)
            ]));

        viewModel.DraftMessage = "hei bo";

        var handled = viewModel.TryAutocompleteNick(viewModel.DraftMessage.Length, out var caretIndex);

        Assert.True(handled);
        Assert.Equal("hei bob", viewModel.DraftMessage);
        Assert.Equal(7, caretIndex);
    }

    [Fact]
    public void NickAutocomplete_CompletesInlineTokenContainingCaret()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(54), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("bob", string.Empty)
            ]));

        viewModel.DraftMessage = "hei bo der";

        var handled = viewModel.TryAutocompleteNick(5, out var caretIndex);

        Assert.True(handled);
        Assert.Equal("hei bob der", viewModel.DraftMessage);
        Assert.Equal(7, caretIndex);
    }

    [Fact]
    public void NickAutocomplete_RepeatedTabCyclesAlphabeticallyThroughMatches()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(52), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("bobby", "o"),
                new QuasselChannelUser("ben", string.Empty),
                new QuasselChannelUser("bob", "v")
            ]));

        viewModel.DraftMessage = "b";

        Assert.True(viewModel.TryAutocompleteNick(1, out var firstCaretIndex));
        Assert.Equal("ben: ", viewModel.DraftMessage);
        Assert.Equal(5, firstCaretIndex);

        Assert.True(viewModel.TryAutocompleteNick(firstCaretIndex, out var secondCaretIndex));
        Assert.Equal("bob: ", viewModel.DraftMessage);
        Assert.Equal(5, secondCaretIndex);

        Assert.True(viewModel.TryAutocompleteNick(secondCaretIndex, out var thirdCaretIndex));
        Assert.Equal("bobby: ", viewModel.DraftMessage);
        Assert.Equal(7, thirdCaretIndex);
    }

    [Fact]
    public void NickAutocomplete_RepeatedTabWrapsBackToFirstMatch()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(53), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("bob", string.Empty),
                new QuasselChannelUser("ben", string.Empty)
            ]));

        viewModel.DraftMessage = "b";

        Assert.True(viewModel.TryAutocompleteNick(1, out var caretIndex));
        Assert.Equal("ben: ", viewModel.DraftMessage);

        Assert.True(viewModel.TryAutocompleteNick(caretIndex, out caretIndex));
        Assert.Equal("bob: ", viewModel.DraftMessage);

        Assert.True(viewModel.TryAutocompleteNick(caretIndex, out _));
        Assert.Equal("ben: ", viewModel.DraftMessage);
    }

    [Fact]
    public void NickAutocomplete_RepeatedInlineTabCyclesAndPreservesSuffix()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(55), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer], [new NetworkId(1)]));
        session.EmitChannelState(new QuasselChannelState(
            new NetworkId(1),
            "#quassel",
            "Current topic",
            [
                new QuasselChannelUser("bobby", "o"),
                new QuasselChannelUser("ben", string.Empty),
                new QuasselChannelUser("bob", "v")
            ]));

        viewModel.DraftMessage = "hei b der";

        Assert.True(viewModel.TryAutocompleteNick(5, out var firstCaretIndex));
        Assert.Equal("hei ben der", viewModel.DraftMessage);
        Assert.Equal(7, firstCaretIndex);

        Assert.True(viewModel.TryAutocompleteNick(firstCaretIndex, out var secondCaretIndex));
        Assert.Equal("hei bob der", viewModel.DraftMessage);
        Assert.Equal(7, secondCaretIndex);

        Assert.True(viewModel.TryAutocompleteNick(secondCaretIndex, out var thirdCaretIndex));
        Assert.Equal("hei bobby der", viewModel.DraftMessage);
        Assert.Equal(9, thirdCaretIndex);
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
        Assert.Empty(session.DeletedBuffers);
        Assert.DoesNotContain(viewModel.Networks.Single().Buffers, buffer => buffer.DisplayName == "#beta");
    }

    [Fact]
    public async Task SelfPartMessage_RemovesChannelBufferFromList()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(14), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var queryBuffer = new QuasselBufferInfo(new BufferId(15), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer, queryBuffer], [new NetworkId(1)]));
        var targetBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "#quassel");

        await viewModel.LeaveChannelBufferCommand.ExecuteAsync(targetBuffer);

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
        Assert.Single(session.DeletedBuffers);
        Assert.Equal(channelBuffer.BufferId, session.DeletedBuffers[0].BufferId);
    }

    [Fact]
    public void SelfPartMessage_DoesNotLeavePartEventInChannelMessages()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var channelBuffer = new QuasselBufferInfo(new BufferId(18), new NetworkId(1), QuasselBufferType.Channel, 0, "#quassel");
        var queryBuffer = new QuasselBufferInfo(new BufferId(19), new NetworkId(1), QuasselBufferType.Query, 0, "bob");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [channelBuffer, queryBuffer], [new NetworkId(1)]));

        var channel = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "#quassel");
        session.EmitMessage(new QuasselMessage(
            new MsgId(31),
            DateTimeOffset.Parse("2026-03-28T13:31:00+01:00"),
            channelBuffer,
            QuasselMessageType.Part,
            "Oddi has left #quassel",
            "Oddi!user@example",
            QuasselMessageFlags.Self));

        Assert.Empty(channel.Messages);
    }

    [Fact]
    public async Task LeaveChannelBuffer_IgnoresLaterBufferUpdatesForSameChannel()
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
        session.EmitBufferInfo(secondBuffer);

        Assert.DoesNotContain(viewModel.Networks.Single().Buffers, buffer => buffer.DisplayName == "#beta");
    }

    [Fact]
    public async Task JoinCommand_RestoresSuppressedChannelBuffer()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Status, 0, "status");
        var channelBuffer = new QuasselBufferInfo(new BufferId(9), new NetworkId(1), QuasselBufferType.Channel, 0, "#beta");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer, channelBuffer], [new NetworkId(1)]));
        var targetBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "#beta");

        await viewModel.LeaveChannelBufferCommand.ExecuteAsync(targetBuffer);
        viewModel.SelectedBuffer = viewModel.Networks.Single().Buffers.Single(buffer => buffer.DisplayName == "status");
        viewModel.DraftMessage = "/join #beta";

        await viewModel.SendMessageCommand.ExecuteAsync(null);
        session.EmitBufferInfo(channelBuffer);

        Assert.Contains(viewModel.Networks.Single().Buffers, buffer => buffer.DisplayName == "#beta");
        Assert.Equal("#beta", viewModel.SelectedBuffer?.DisplayName);
    }

    [Fact]
    public async Task JoinCommand_SelectsNewChannelBufferWhenItAppears()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(40), new NetworkId(1), QuasselBufferType.Status, 0, "status");
        var channelBuffer = new QuasselBufferInfo(new BufferId(41), new NetworkId(1), QuasselBufferType.Channel, 0, "#newroom");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        viewModel.DraftMessage = "/join #newroom";

        await viewModel.SendMessageCommand.ExecuteAsync(null);
        session.EmitBufferInfo(channelBuffer);

        Assert.Equal("#newroom", viewModel.SelectedBuffer?.DisplayName);
    }

    [Fact]
    public async Task ShortJoinCommand_SelectsNewChannelBufferWhenItAppears()
    {
        var session = new FakeSessionService();
        var settings = new FakeSettingsStore(new StoredConnectionSettings(Host: "chat.example", Username: "alice"));
        var viewModel = new MainWindowViewModel(session, settings, marshalToUiThread: false);
        var statusBuffer = new QuasselBufferInfo(new BufferId(42), new NetworkId(1), QuasselBufferType.Status, 0, "status");
        var channelBuffer = new QuasselBufferInfo(new BufferId(43), new NetworkId(1), QuasselBufferType.Channel, 0, "#testkanal");

        session.EmitConnectionState(QuasselConnectionState.Ready, "Connected");
        session.EmitSessionState(new QuasselSessionState([], [statusBuffer], [new NetworkId(1)]));

        viewModel.DraftMessage = "/j testkanal";

        await viewModel.SendMessageCommand.ExecuteAsync(null);
        session.EmitBufferInfo(channelBuffer);

        Assert.Equal("#testkanal", viewModel.SelectedBuffer?.DisplayName);
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
        public ConnectionSettingsLoadResult? LoadResult { get; init; }
        public ConnectionSettingsSaveResult NextSaveResult { get; set; } = ConnectionSettingsSaveResult.Saved();

        public ConnectionSettingsLoadResult Load() => LoadResult ?? ConnectionSettingsLoadResult.Loaded(_settings);

        public ConnectionSettingsSaveResult Save(StoredConnectionSettings settings)
        {
            var result = NextSaveResult;
            if (result.Status != ConnectionSettingsSaveStatus.Failed)
            {
                _settings = settings;
            }

            return result;
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
        public event Action<MessageCacheOperationResult>? MessageCacheOperationCompleted;
        public event Action<NetworkId>? NetworkRemoved;

        public List<(QuasselBufferInfo bufferInfo, int amount)> BacklogRequests { get; } = [];
        public List<(QuasselBufferInfo bufferInfo, string text)> SentInputs { get; } = [];
        public List<QuasselBufferInfo> DeletedBuffers { get; } = [];
        public List<ConnectionProfile> ConnectRequests { get; } = [];
        public Dictionary<BufferId, IReadOnlyList<QuasselMessage>> CachedMessages { get; } = [];

        public Task ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
        {
            ConnectRequests.Add(profile);
            return Task.CompletedTask;
        }
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendInputAsync(QuasselBufferInfo bufferInfo, string text, CancellationToken cancellationToken = default)
        {
            SentInputs.Add((bufferInfo, text));
            return Task.CompletedTask;
        }

        public MessageCacheLoadResult GetCachedMessages(QuasselBufferInfo bufferInfo, int amount = 120)
        {
            var messages = CachedMessages.TryGetValue(bufferInfo.BufferId, out var cachedMessages)
                ? cachedMessages.TakeLast(amount).ToArray()
                : Array.Empty<QuasselMessage>();
            return new MessageCacheLoadResult(messages, MessageCacheOperationStatus.Available);
        }

        public Task EnsureBacklogAsync(QuasselBufferInfo bufferInfo, int amount = 120, CancellationToken cancellationToken = default)
        {
            BacklogRequests.Add((bufferInfo, amount));
            return Task.CompletedTask;
        }

        public Task DeleteBufferAsync(QuasselBufferInfo bufferInfo, CancellationToken cancellationToken = default)
        {
            DeletedBuffers.Add(bufferInfo);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void EmitConnectionState(QuasselConnectionState state, string? message) => ConnectionStateChanged?.Invoke(state, message);
        public void EmitSessionState(QuasselSessionState state) => SessionStateReceived?.Invoke(state);
        public void EmitStatus(string message) => StatusReceived?.Invoke(message);
        public void EmitMessageCacheOperation(MessageCacheOperationResult result) => MessageCacheOperationCompleted?.Invoke(result);
        public void EmitNetworkState(QuasselNetworkState state) => NetworkStateReceived?.Invoke(state);
        public void EmitBufferInfo(QuasselBufferInfo bufferInfo) => BufferInfoUpdated?.Invoke(bufferInfo);
        public void EmitChannelState(QuasselChannelState state) => ChannelStateReceived?.Invoke(state);
        public void EmitChannelTopic(QuasselChannelTopicUpdate topic) => ChannelTopicReceived?.Invoke(topic);
        public void EmitMessage(QuasselMessage message) => MessageReceived?.Invoke(message);
        public void EmitNetworkRemoved(NetworkId networkId) => NetworkRemoved?.Invoke(networkId);
    }
}
