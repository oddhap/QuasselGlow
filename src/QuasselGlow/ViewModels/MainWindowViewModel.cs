using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuasselGlow.Appearance;
using QuasselGlow.Localization;
using Quassel.Client.Domain;
using Quassel.Client.Infrastructure;

namespace QuasselGlow.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IQuasselSessionService _session;
    private readonly IConnectionSettingsStore _settingsStore;
    private readonly bool _marshalToUiThread;
    private readonly UiTextCatalog _strings = UiTextCatalog.Instance;
    private readonly Dictionary<NetworkId, NetworkItemViewModel> _networksById = new();
    private readonly Dictionary<BufferId, BufferItemViewModel> _buffersById = new();
    private readonly Dictionary<string, QuasselChannelState> _channelStatesByKey = new(StringComparer.OrdinalIgnoreCase);

    private QuasselConnectionState _connectionState = QuasselConnectionState.Disconnected;
    private BufferItemViewModel? _selectedBuffer;
    private bool _isApplyingStoredSettings;
    private bool _statusUsesCustomText;
    private string? _lastConnectionStateMessage;
    private string _statusDetailOverride = string.Empty;
    private string _selectedLanguageCode = UiTextCatalog.Instance.CurrentLanguageCode;
    private string _selectedThemeKey = AppThemeCatalog.DefaultThemeKey;
    private string _selectedThemeModeKey = AppThemeCatalog.DefaultModeKey;
    private readonly ObservableCollection<AppDisplayOption> _supportedThemes = [];
    private readonly ObservableCollection<AppDisplayOption> _supportedThemeModes = [];
    private bool _canAcknowledgeSelectedBuffer = true;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _portText = "60096";

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _trustInvalidCertificates;

    [ObservableProperty]
    private bool _rememberLogin;

    [ObservableProperty]
    private bool _isConnectionEditorOpen;

    [ObservableProperty]
    private bool _isThemeEditorOpen;

    [ObservableProperty]
    private bool _isControlPanelOpen;

    [ObservableProperty]
    private bool _isUserListPinned;

    [ObservableProperty]
    private string _statusText = UiTextCatalog.Instance["StatusDisconnected"];

    [ObservableProperty]
    private string _draftMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _minimizeToTrayEnabled;

    public MainWindowViewModel()
        : this(new QuasselSessionService(), new LocalConnectionSettingsStore())
    {
    }

    public MainWindowViewModel(IQuasselSessionService session, IConnectionSettingsStore settingsStore, bool marshalToUiThread = true)
    {
        _session = session;
        _settingsStore = settingsStore;
        _marshalToUiThread = marshalToUiThread;
        _isApplyingStoredSettings = true;
        try
        {
            ApplyStoredSettings(_settingsStore.Load());
        }
        finally
        {
            _isApplyingStoredSettings = false;
        }

        RefreshAppearanceOptions();
        ApplyAppearance();
        StatusText = BuildConnectionStatusText(_connectionState, _lastConnectionStateMessage);

        _session.ConnectionStateChanged += OnConnectionStateChanged;
        _session.SessionStateReceived += OnSessionStateReceived;
        _session.NetworkStateReceived += state => RunOnUiThread(() => UpsertNetwork(state));
        _session.BufferInfoUpdated += info => RunOnUiThread(() => UpsertBuffer(info));
        _session.ChannelStateReceived += state => RunOnUiThread(() => ApplyChannelState(state));
        _session.ChannelTopicReceived += topic => RunOnUiThread(() => ApplyChannelTopic(topic));
        _session.MessageReceived += message => RunOnUiThread(() => ApplyMessage(message));
        _session.StatusReceived += OnStatusReceived;
        _session.NetworkRemoved += networkId => RunOnUiThread(() => RemoveNetwork(networkId));
    }

    public UiTextCatalog Strings => _strings;

    public IReadOnlyList<UiLanguageOption> SupportedLanguages => _strings.SupportedLanguages;

    public ObservableCollection<AppDisplayOption> SupportedThemes => _supportedThemes;

    public ObservableCollection<AppDisplayOption> SupportedThemeModes => _supportedThemeModes;

    public UiLanguageOption? SelectedLanguage
    {
        get => SupportedLanguages.FirstOrDefault(option => string.Equals(option.Code, SelectedLanguageCode, StringComparison.OrdinalIgnoreCase));
        set => SelectedLanguageCode = value?.Code ?? UiTextCatalog.DefaultLanguageCode;
    }

    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            var resolved = UiTextCatalog.ResolveLanguageCode(value);
            if (string.Equals(_selectedLanguageCode, resolved, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _strings.SetLanguage(resolved);
            if (SetProperty(ref _selectedLanguageCode, resolved))
            {
                OnPropertyChanged(nameof(SelectedLanguage));
                RefreshLocalizedText();
                SaveSettingsIfReady();
            }
        }
    }

    public AppDisplayOption? SelectedTheme
    {
        get => SupportedThemes.FirstOrDefault(option => string.Equals(option.Key, SelectedThemeKey, StringComparison.OrdinalIgnoreCase));
        set => SelectedThemeKey = value?.Key ?? AppThemeCatalog.DefaultThemeKey;
    }

    public string SelectedThemeKey
    {
        get => _selectedThemeKey;
        set
        {
            var resolved = AppThemeCatalog.NormalizeThemeKey(value);
            if (string.Equals(_selectedThemeKey, resolved, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (SetProperty(ref _selectedThemeKey, resolved))
            {
                OnPropertyChanged(nameof(SelectedTheme));
                OnPropertyChanged(nameof(ThemeSummaryText));
                ApplyAppearance();
                SaveSettingsIfReady();
            }
        }
    }

    public AppDisplayOption? SelectedThemeMode
    {
        get => SupportedThemeModes.FirstOrDefault(option => string.Equals(option.Key, SelectedThemeModeKey, StringComparison.OrdinalIgnoreCase));
        set => SelectedThemeModeKey = value?.Key ?? AppThemeCatalog.DefaultModeKey;
    }

    public string SelectedThemeModeKey
    {
        get => _selectedThemeModeKey;
        set
        {
            var resolved = AppThemeCatalog.NormalizeModeKey(value);
            if (string.Equals(_selectedThemeModeKey, resolved, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (SetProperty(ref _selectedThemeModeKey, resolved))
            {
                OnPropertyChanged(nameof(SelectedThemeMode));
                OnPropertyChanged(nameof(ThemeSummaryText));
                ApplyAppearance();
                SaveSettingsIfReady();
            }
        }
    }

    public ObservableCollection<NetworkItemViewModel> Networks { get; } = [];

    public BufferItemViewModel? SelectedBuffer
    {
        get => _selectedBuffer;
        set
        {
            if (_selectedBuffer == value)
            {
                return;
            }

            if (_selectedBuffer is not null)
            {
                _selectedBuffer.IsSelected = false;
            }

            if (SetProperty(ref _selectedBuffer, value))
            {
                if (_selectedBuffer is not null)
                {
                    _selectedBuffer.IsSelected = true;
                    if (_canAcknowledgeSelectedBuffer)
                    {
                        _selectedBuffer.MarkRead();
                    }

                    _ = EnsureBacklogForSelectionAsync(_selectedBuffer);
                }

                OnPropertyChanged(nameof(SelectedNetwork));
                OnPropertyChanged(nameof(CanSendMessage));
                SendMessageCommand.NotifyCanExecuteChanged();
                RaiseSelectionTextPropertiesChanged();
            }
        }
    }

    public NetworkItemViewModel? SelectedNetwork =>
        SelectedBuffer is not null && _networksById.TryGetValue(SelectedBuffer.BufferInfo.NetworkId, out var network)
            ? network
            : null;

    public bool CanSendMessage => !IsBusy && SelectedBuffer?.AcceptsInput == true && !string.IsNullOrWhiteSpace(DraftMessage);

    public bool IsConnected => _connectionState is not QuasselConnectionState.Disconnected and not QuasselConnectionState.Error;

    public bool ShowConnectAction => !IsConnected;

    public bool ShowDisconnectAction => IsConnected;

    public string ConnectionEndpointText
    {
        get
        {
            var trimmedHost = Host.Trim();
            return string.IsNullOrWhiteSpace(trimmedHost)
                ? _strings.Format("NoHostWithPort", GetEndpointPortText())
                : $"{trimmedHost}:{GetEndpointPortText()}";
        }
    }

    public string ConnectionIdentityText => string.IsNullOrWhiteSpace(Username) ? _strings["NoUser"] : Username;

    public string TlsModeText => TrustInvalidCertificates ? _strings["InsecureTls"] : _strings["Tls"];

    public string ThemeSummaryText =>
        $"{SelectedTheme?.DisplayName ?? _strings["ThemeLabel"]} / {SelectedThemeMode?.DisplayName ?? _strings["ThemeModeLabel"]}";

    public string CurrentSelectionText => SelectedBuffer?.DisplayName ?? _strings["SelectBufferToStart"];

    public string CurrentNetworkText => SelectedNetwork?.DisplayName ?? _strings["NoNetworkSelected"];

    public string SelectedBufferHeadingText => SelectedBuffer?.DisplayName ?? _strings["NoBufferSelected"];

    public string SelectedNetworkStatusText
    {
        get
        {
            var baseStatus = SelectedNetwork?.StatusText ?? _strings["SelectChannelOrQuery"];
            var alertSummary = BuildPendingAlertSummary();
            return string.IsNullOrWhiteSpace(alertSummary)
                ? baseStatus
                : $"{baseStatus} | {alertSummary}";
        }
    }

    public string SelectedBufferSubtitleText
    {
        get
        {
            if (SelectedBuffer is null)
            {
                return _strings["SelectChannelOrQuery"];
            }

            return SelectedBuffer.BufferInfo.Type switch
            {
                QuasselBufferType.Channel => SelectedBuffer.ChannelTopic,
                QuasselBufferType.Query => string.Empty,
                _ => SelectedNetworkStatusText
            };
        }
    }

    public bool ShowSelectedBufferSubtitle => !string.IsNullOrWhiteSpace(SelectedBufferSubtitleText);

    public string SelectedNickText => string.IsNullOrWhiteSpace(SelectedNetwork?.MyNick) ? "nick" : SelectedNetwork.MyNick;

    public bool SelectedBufferSupportsUserList => SelectedBuffer?.BufferInfo.Type == QuasselBufferType.Channel;

    public IReadOnlyList<ChannelUserViewModel> SelectedChannelUsers => SelectedBuffer?.ChannelUsers ?? [];

    public bool ShowSelectedChannelUsers => SelectedBufferSupportsUserList && SelectedBuffer?.MemberCount > 0;

    public bool ShowSelectedChannelUsersEmptyState => SelectedBufferSupportsUserList && SelectedBuffer?.MemberCount == 0;

    public bool ShowSelectedChannelUsersUnavailable => SelectedBuffer is not null && !SelectedBufferSupportsUserList;

    public SplitViewDisplayMode UserListDisplayMode => IsUserListPinned ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;

    public bool UseOverlayDismissForUserList => !IsUserListPinned;

    public string UserListPinButtonText => _strings[IsUserListPinned ? "Unpin" : "Pin"];

    public string UserListStatusText
    {
        get
        {
            if (SelectedBuffer is null)
            {
                return _strings["SelectChannelOrQuery"];
            }

            if (!SelectedBufferSupportsUserList)
            {
                return _strings["UsersOnlyForChannels"];
            }

            return SelectedBuffer.MemberCount == 0
                ? _strings["NoUsersLoaded"]
                : _strings.Format("UsersCountFormat", SelectedBuffer.MemberCount);
        }
    }

    public string ControlPanelNetworkNameText => SelectedNetwork?.DisplayName ?? _strings["NoneSelected"];

    public string ControlPanelServerText =>
        string.IsNullOrWhiteSpace(SelectedNetwork?.CurrentServer) ? _strings["ServerNotLoaded"] : SelectedNetwork.CurrentServer;

    public string ControlPanelNickText => string.IsNullOrWhiteSpace(SelectedNetwork?.MyNick) ? _strings["NickNotKnown"] : SelectedNetwork.MyNick;

    public string ControlPanelBufferNameText => SelectedBuffer?.DisplayName ?? _strings["SelectBuffer"];

    public string ControlPanelBufferPreviewText =>
        string.IsNullOrWhiteSpace(SelectedBuffer?.LastMessagePreview) ? _strings["NoMessagesYet"] : SelectedBuffer.LastMessagePreview;

    public string ComposerContextText => SelectedBuffer?.DisplayName ?? _strings["SelectChannelOrQuery"];

    public string TrayToolTipText
    {
        get
        {
            var summary = BuildPendingAlertSummary();
            var content = string.IsNullOrWhiteSpace(summary) ? StatusText : summary;
            return $"QuasselGlow | {content}";
        }
    }

    public string ConnectionStatusDetailText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_statusDetailOverride)
                && !string.Equals(_statusDetailOverride, StatusText, StringComparison.Ordinal))
            {
                return _statusDetailOverride;
            }

            if (!string.IsNullOrWhiteSpace(_lastConnectionStateMessage)
                && !string.Equals(_lastConnectionStateMessage, StatusText, StringComparison.Ordinal))
            {
                return _lastConnectionStateMessage;
            }

            var trimmedHost = Host.Trim();
            var trimmedUser = Username.Trim();
            if (string.IsNullOrWhiteSpace(trimmedHost))
            {
                return _strings["StatusReadyToConnectDetail"];
            }

            return string.IsNullOrWhiteSpace(trimmedUser)
                ? _strings.Format("ConnectionSummaryEndpointOnly", ConnectionEndpointText)
                : _strings.Format("ConnectionSummaryWithIdentity", ConnectionEndpointText, trimmedUser);
        }
    }

    public string SessionSummaryText
    {
        get
        {
            if (!IsConnected)
            {
                return _strings["SessionSummaryDisconnected"];
            }

            if (_networksById.Count == 0 && _buffersById.Count == 0)
            {
                return _strings["SessionSummaryWaitingForSync"];
            }

            return _strings.Format("SessionSummaryFormat", _networksById.Count, _buffersById.Count);
        }
    }

    public IBrush ConnectionBrush => _connectionState switch
    {
        QuasselConnectionState.Ready => new SolidColorBrush(Color.Parse("#0F8C63")),
        QuasselConnectionState.Error => new SolidColorBrush(Color.Parse("#C2410C")),
        QuasselConnectionState.Disconnected => new SolidColorBrush(Color.Parse("#64748B")),
        _ => new SolidColorBrush(Color.Parse("#0F766E"))
    };

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        try
        {
            var trimmedHost = Host.Trim();
            if (string.IsNullOrWhiteSpace(trimmedHost))
            {
                throw new InvalidOperationException(_strings["HostCannotBeEmpty"]);
            }

            var port = GetPortForConnection();
            await _session.ConnectAsync(new ConnectionProfile(trimmedHost, port, Username.Trim(), Password, TrustInvalidCertificates))
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsConnectionEditorOpen = false;
                SaveSettings();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _statusUsesCustomText = true;
                StatusText = ex.Message;
                _statusDetailOverride = string.Empty;
                OnPropertyChanged(nameof(ConnectionStatusDetailText));
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                NotifyCommandState();
            });
        }
    }

    private bool CanConnect() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await _session.DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                NotifyCommandState();
            });
        }
    }

    private bool CanDisconnect() => !IsBusy && _connectionState != QuasselConnectionState.Disconnected;

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (SelectedBuffer is null)
        {
            return;
        }

        var text = DraftMessage.Trim();
        if (text.Length == 0)
        {
            return;
        }

        DraftMessage = string.Empty;
        await _session.SendInputAsync(SelectedBuffer.BufferInfo, text).ConfigureAwait(false);
    }

    [RelayCommand]
    private Task GiveOpToChannelUserAsync(ChannelUserViewModel? user)
    {
        return SendModeChangeForChannelUserAsync(user, "+o");
    }

    [RelayCommand]
    private Task RemoveOpFromChannelUserAsync(ChannelUserViewModel? user)
    {
        return SendModeChangeForChannelUserAsync(user, "-o");
    }

    [RelayCommand]
    private Task GiveVoiceToChannelUserAsync(ChannelUserViewModel? user)
    {
        return SendModeChangeForChannelUserAsync(user, "+v");
    }

    [RelayCommand]
    private Task RemoveVoiceFromChannelUserAsync(ChannelUserViewModel? user)
    {
        return SendModeChangeForChannelUserAsync(user, "-v");
    }

    [RelayCommand]
    private Task KickChannelUserAsync(ChannelUserViewModel? user)
    {
        return SendChannelCommandForUserAsync(user, channelName => $"/kick {channelName} {user!.Nick}");
    }

    [RelayCommand]
    private Task BanChannelUserAsync(ChannelUserViewModel? user)
    {
        return SendChannelCommandForUserAsync(user, channelName => $"/mode {channelName} +b {BuildNickBanMask(user!.Nick)}");
    }

    [RelayCommand]
    private async Task KickBanChannelUserAsync(ChannelUserViewModel? user)
    {
        if (!TryGetSelectedChannelBuffer(out var channelBuffer) || user is null)
        {
            return;
        }

        var bufferInfo = channelBuffer;
        var channelName = bufferInfo.BufferName;
        var banMask = BuildNickBanMask(user.Nick);
        await _session.SendInputAsync(bufferInfo, $"/mode {channelName} +b {banMask}").ConfigureAwait(false);
        await _session.SendInputAsync(bufferInfo, $"/kick {channelName} {user.Nick}").ConfigureAwait(false);
    }

    [RelayCommand]
    private void MarkBufferAsRead(BufferItemViewModel? buffer)
    {
        buffer?.MarkRead();
    }

    [RelayCommand]
    private Task RejoinChannelBufferAsync(BufferItemViewModel? buffer)
    {
        return SendBufferCommandAsync(buffer, QuasselBufferType.Channel, static bufferInfo => $"/join {bufferInfo.BufferName}");
    }

    [RelayCommand]
    private Task LeaveChannelBufferAsync(BufferItemViewModel? buffer)
    {
        return SendBufferCommandAsync(buffer, QuasselBufferType.Channel, static bufferInfo => $"/part {bufferInfo.BufferName}");
    }

    [RelayCommand]
    private Task WhoisBufferAsync(BufferItemViewModel? buffer)
    {
        return SendBufferCommandAsync(buffer, QuasselBufferType.Query, static bufferInfo => $"/whois {bufferInfo.BufferName}");
    }

    [RelayCommand]
    private void SelectBuffer(BufferItemViewModel? buffer)
    {
        SelectedBuffer = buffer;
    }

    [RelayCommand]
    private void ToggleControlPanel()
    {
        IsControlPanelOpen = !IsControlPanelOpen;
    }

    [RelayCommand]
    private void ToggleUserListPinned()
    {
        var shouldPin = !IsUserListPinned;
        IsUserListPinned = shouldPin;
        if (shouldPin)
        {
            IsControlPanelOpen = true;
        }
    }

    [RelayCommand]
    private void ToggleConnectionEditor()
    {
        var shouldOpen = !IsConnectionEditorOpen;
        if (shouldOpen)
        {
            IsThemeEditorOpen = false;
        }

        IsConnectionEditorOpen = shouldOpen;
    }

    [RelayCommand]
    private void CloseConnectionEditor()
    {
        IsConnectionEditorOpen = false;
    }

    [RelayCommand]
    private void ToggleThemeEditor()
    {
        var shouldOpen = !IsThemeEditorOpen;
        if (shouldOpen)
        {
            IsConnectionEditorOpen = false;
        }

        IsThemeEditorOpen = shouldOpen;
    }

    [RelayCommand]
    private void CloseThemeEditor()
    {
        IsThemeEditorOpen = false;
    }

    public async ValueTask DisposeAsync()
    {
        SaveSettings();
        await _session.DisposeAsync().ConfigureAwait(false);
    }

    partial void OnDraftMessageChanged(string value)
    {
        OnPropertyChanged(nameof(CanSendMessage));
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    partial void OnRememberLoginChanged(bool value)
    {
        SaveSettingsIfReady();
    }

    partial void OnMinimizeToTrayEnabledChanged(bool value)
    {
        SaveSettingsIfReady();
    }

    partial void OnTrustInvalidCertificatesChanged(bool value)
    {
        SaveSettingsIfReady();
        OnPropertyChanged(nameof(TlsModeText));
    }

    partial void OnIsControlPanelOpenChanged(bool value)
    {
        SaveSettingsIfReady();
    }

    partial void OnIsUserListPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(UserListDisplayMode));
        OnPropertyChanged(nameof(UseOverlayDismissForUserList));
        OnPropertyChanged(nameof(UserListPinButtonText));
        SaveSettingsIfReady();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyCommandState();
        OnPropertyChanged(nameof(CanSendMessage));
        OnPropertyChanged(nameof(ConnectionBrush));
    }

    partial void OnHostChanged(string value)
    {
        OnPropertyChanged(nameof(ConnectionEndpointText));
        OnPropertyChanged(nameof(ConnectionStatusDetailText));
        SaveSettingsIfReady();
    }

    partial void OnPortTextChanged(string value)
    {
        OnPropertyChanged(nameof(ConnectionEndpointText));
        OnPropertyChanged(nameof(ConnectionStatusDetailText));
        SaveSettingsIfReady();
    }

    partial void OnUsernameChanged(string value)
    {
        OnPropertyChanged(nameof(ConnectionIdentityText));
        OnPropertyChanged(nameof(ConnectionStatusDetailText));
        SaveSettingsIfReady();
    }

    partial void OnPasswordChanged(string value)
    {
        SaveSettingsIfReady();
    }

    private void OnConnectionStateChanged(QuasselConnectionState state, string? message)
    {
        RunOnUiThread(() =>
        {
            _connectionState = state;
            _lastConnectionStateMessage = message;
            _statusDetailOverride = string.Empty;
            _statusUsesCustomText = state == QuasselConnectionState.Error && !string.IsNullOrWhiteSpace(message);
            StatusText = _statusUsesCustomText
                ? message!
                : BuildConnectionStatusText(state, message);

            OnPropertyChanged(nameof(ConnectionBrush));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ShowConnectAction));
            OnPropertyChanged(nameof(ShowDisconnectAction));
            OnPropertyChanged(nameof(TrayToolTipText));
            OnPropertyChanged(nameof(ConnectionStatusDetailText));
            OnPropertyChanged(nameof(SessionSummaryText));
            NotifyCommandState();

            if (state == QuasselConnectionState.Error)
            {
                IsThemeEditorOpen = false;
                IsConnectionEditorOpen = true;
            }

            if (state is QuasselConnectionState.Disconnected or QuasselConnectionState.Error)
            {
                DetachFromAllBuffers();
                Networks.Clear();
                _networksById.Clear();
                _buffersById.Clear();
                _channelStatesByKey.Clear();
                SelectedBuffer = null;
                OnPropertyChanged(nameof(SessionSummaryText));
            }
        });
    }

    private void OnStatusReceived(string message)
    {
        RunOnUiThread(() => ApplyExternalStatus(message));
    }

    private void ApplyExternalStatus(string message)
    {
        _statusDetailOverride = message;
        if (!_statusUsesCustomText)
        {
            StatusText = BuildConnectionStatusText(_connectionState, _lastConnectionStateMessage);
        }

        OnPropertyChanged(nameof(TrayToolTipText));
        OnPropertyChanged(nameof(ConnectionStatusDetailText));
    }

    private void OnSessionStateReceived(QuasselSessionState sessionState)
    {
        RunOnUiThread(() =>
        {
            DetachFromAllBuffers();
            Networks.Clear();
            _networksById.Clear();
            _buffersById.Clear();
            _channelStatesByKey.Clear();

            foreach (var networkId in sessionState.Networks)
            {
                EnsureNetwork(networkId);
            }

            foreach (var buffer in sessionState.Buffers)
            {
                UpsertBuffer(buffer);
            }

            SelectedBuffer ??= PickInitialBuffer();
            OnPropertyChanged(nameof(TrayToolTipText));
            OnPropertyChanged(nameof(SessionSummaryText));
        });
    }

    private void UpsertNetwork(QuasselNetworkState state)
    {
        var network = EnsureNetwork(state.NetworkId);
        network.Apply(state);

        if (SelectedNetwork?.NetworkId == state.NetworkId)
        {
            RaiseSelectionTextPropertiesChanged();
        }
    }

    private NetworkItemViewModel EnsureNetwork(NetworkId networkId)
    {
        if (_networksById.TryGetValue(networkId, out var existing))
        {
            return existing;
        }

        var created = new NetworkItemViewModel(networkId);
        _networksById[networkId] = created;
        var insertAt = Networks.TakeWhile(item => item.NetworkId.Value < networkId.Value).Count();
        Networks.Insert(insertAt, created);
        OnPropertyChanged(nameof(SessionSummaryText));
        return created;
    }

    private void UpsertBuffer(QuasselBufferInfo bufferInfo)
    {
        var network = EnsureNetwork(bufferInfo.NetworkId);
        if (_buffersById.TryGetValue(bufferInfo.BufferId, out var existing))
        {
            existing.UpdateInfo(bufferInfo);
            ApplyCachedChannelState(existing);
            if (SelectedBuffer?.BufferInfo.BufferId == bufferInfo.BufferId)
            {
                RaiseSelectionTextPropertiesChanged();
            }

            return;
        }

        var created = new BufferItemViewModel(bufferInfo);
        _buffersById[bufferInfo.BufferId] = created;
        created.PropertyChanged += OnBufferPropertyChanged;
        ApplyCachedChannelState(created);
        network.UpsertBuffer(created);

        SelectedBuffer ??= PickInitialBuffer();
        OnPropertyChanged(nameof(TrayToolTipText));
        OnPropertyChanged(nameof(SessionSummaryText));
    }

    private void ApplyMessage(QuasselMessage message)
    {
        if (!_buffersById.TryGetValue(message.BufferInfo.BufferId, out var existingBuffer)
            && IsChannelBufferRemovalMessage(message))
        {
            return;
        }

        var buffer = existingBuffer;
        if (buffer is null)
        {
            UpsertBuffer(message.BufferInfo);
            if (!_buffersById.TryGetValue(message.BufferInfo.BufferId, out buffer))
            {
                return;
            }
        }

        var shouldTrackUnread = SelectedBuffer?.BufferInfo.BufferId != buffer.BufferInfo.BufferId || !_canAcknowledgeSelectedBuffer;
        buffer.AddMessage(message, shouldTrackUnread);
        if (SelectedBuffer?.BufferInfo.BufferId == buffer.BufferInfo.BufferId)
        {
            RaiseSelectionTextPropertiesChanged();
        }

        if (IsChannelBufferRemovalMessage(message))
        {
            RemoveBuffer(buffer);
            return;
        }

        OnPropertyChanged(nameof(TrayToolTipText));
    }

    private void RemoveBuffer(BufferItemViewModel buffer)
    {
        if (!_buffersById.Remove(buffer.BufferInfo.BufferId))
        {
            return;
        }

        buffer.PropertyChanged -= OnBufferPropertyChanged;

        if (_networksById.TryGetValue(buffer.BufferInfo.NetworkId, out var network))
        {
            network.RemoveBuffer(buffer.BufferInfo.BufferId);
        }

        if (buffer.BufferInfo.Type == QuasselBufferType.Channel)
        {
            _channelStatesByKey.Remove(BuildChannelStateKey(buffer.BufferInfo.NetworkId, buffer.BufferInfo.BufferName));
        }

        if (SelectedBuffer?.BufferInfo.BufferId == buffer.BufferInfo.BufferId)
        {
            SelectedBuffer = PickInitialBuffer();
        }

        RaiseSelectionTextPropertiesChanged();
        OnPropertyChanged(nameof(TrayToolTipText));
        OnPropertyChanged(nameof(SessionSummaryText));
    }

    private void RemoveNetwork(NetworkId networkId)
    {
        if (!_networksById.TryGetValue(networkId, out var network))
        {
            return;
        }

        foreach (var buffer in network.Buffers)
        {
            buffer.PropertyChanged -= OnBufferPropertyChanged;
            _buffersById.Remove(buffer.BufferInfo.BufferId);
        }

        Networks.Remove(network);
        _networksById.Remove(networkId);
        RemoveCachedChannelStates(networkId);

        if (SelectedBuffer is not null && SelectedBuffer.BufferInfo.NetworkId == networkId)
        {
            SelectedBuffer = PickInitialBuffer();
        }

        RaiseSelectionTextPropertiesChanged();
        OnPropertyChanged(nameof(TrayToolTipText));
        OnPropertyChanged(nameof(SessionSummaryText));
    }

    private void ApplyChannelState(QuasselChannelState state)
    {
        var cacheKey = BuildChannelStateKey(state.NetworkId, state.ChannelName);
        _channelStatesByKey[cacheKey] = state;

        var matchingBuffer = _buffersById.Values.FirstOrDefault(buffer =>
            buffer.BufferInfo.Type == QuasselBufferType.Channel
            && buffer.BufferInfo.NetworkId == state.NetworkId
            && string.Equals(buffer.BufferInfo.BufferName, state.ChannelName, StringComparison.OrdinalIgnoreCase));

        if (matchingBuffer is null)
        {
            return;
        }

        matchingBuffer.ApplyChannelState(state);
        if (ReferenceEquals(matchingBuffer, SelectedBuffer))
        {
            RaiseSelectionTextPropertiesChanged();
        }
    }

    private void ApplyChannelTopic(QuasselChannelTopicUpdate topic)
    {
        var cacheKey = BuildChannelStateKey(topic.NetworkId, topic.ChannelName);
        var existingState = _channelStatesByKey.TryGetValue(cacheKey, out var cachedState)
            ? cachedState
            : new QuasselChannelState(topic.NetworkId, topic.ChannelName, string.Empty, Array.Empty<QuasselChannelUser>());
        _channelStatesByKey[cacheKey] = existingState with { Topic = topic.Topic };

        var matchingBuffer = _buffersById.Values.FirstOrDefault(buffer =>
            buffer.BufferInfo.Type == QuasselBufferType.Channel
            && buffer.BufferInfo.NetworkId == topic.NetworkId
            && string.Equals(buffer.BufferInfo.BufferName, topic.ChannelName, StringComparison.OrdinalIgnoreCase));

        if (matchingBuffer is null)
        {
            return;
        }

        matchingBuffer.SetChannelTopic(topic.Topic);
        if (ReferenceEquals(matchingBuffer, SelectedBuffer))
        {
            RaiseSelectionTextPropertiesChanged();
        }
    }

    private BufferItemViewModel? PickInitialBuffer()
    {
        return _buffersById.Values
            .OrderBy(buffer => buffer.BufferInfo.Type == QuasselBufferType.Channel ? 0 : 1)
            .ThenBy(buffer => buffer.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private async Task EnsureBacklogForSelectionAsync(BufferItemViewModel selected)
    {
        try
        {
            await _session.EnsureBacklogAsync(selected.BufferInfo, 150).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _statusDetailOverride = ex.Message;
                OnPropertyChanged(nameof(ConnectionStatusDetailText));
            });
        }
    }

    private void ApplyStoredSettings(StoredConnectionSettings settings)
    {
        SelectedLanguageCode = settings.LanguageCode;
        Host = settings.Host;
        PortText = settings.Port.ToString(CultureInfo.InvariantCulture);
        Username = settings.Username;
        Password = settings.Password;
        TrustInvalidCertificates = settings.TrustInvalidCertificates;
        RememberLogin = settings.RememberLogin;
        IsControlPanelOpen = settings.IsControlPanelOpen;
        IsUserListPinned = settings.IsUserListPinned;
        SelectedThemeKey = settings.ThemeKey;
        SelectedThemeModeKey = settings.ThemeModeKey;
        MinimizeToTrayEnabled = settings.MinimizeToTray;
    }

    private void SaveSettings()
    {
        _settingsStore.Save(new StoredConnectionSettings(
            Host,
            GetPortForSettings(),
            RememberLogin ? Username : string.Empty,
            RememberLogin ? Password : string.Empty,
            TrustInvalidCertificates,
            RememberLogin,
            IsControlPanelOpen,
            IsUserListPinned,
            SelectedLanguageCode,
            SelectedThemeKey,
            SelectedThemeModeKey,
            MinimizeToTrayEnabled));
    }

    private void SaveSettingsIfReady()
    {
        if (_isApplyingStoredSettings)
        {
            return;
        }

        SaveSettings();
    }

    private int GetPortForConnection()
    {
        if (string.IsNullOrWhiteSpace(PortText))
        {
            return 60096;
        }

        if (int.TryParse(PortText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= 1 and <= 65535)
        {
            return port;
        }

        throw new InvalidOperationException(_strings["PortRangeError"]);
    }

    private int GetPortForSettings()
    {
        if (int.TryParse(PortText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= 1 and <= 65535)
        {
            return port;
        }

        return 60096;
    }

    private string GetEndpointPortText()
    {
        return string.IsNullOrWhiteSpace(PortText) ? "60096" : PortText.Trim();
    }

    private string BuildConnectionStatusText(QuasselConnectionState state, string? message)
    {
        return state switch
        {
            QuasselConnectionState.Disconnected when string.Equals(message, "Disconnected by core", StringComparison.Ordinal)
                => _strings["StatusDisconnectedByCore"],
            QuasselConnectionState.Disconnected => _strings["StatusDisconnected"],
            QuasselConnectionState.Connecting => _strings["StatusConnecting"],
            QuasselConnectionState.Negotiating => _strings["StatusNegotiating"],
            QuasselConnectionState.Encrypting => _strings["StatusEncrypting"],
            QuasselConnectionState.Registering => _strings["StatusRegistering"],
            QuasselConnectionState.Authenticating => _strings["StatusAuthenticating"],
            QuasselConnectionState.Synchronizing => _strings["StatusSynchronizing"],
            QuasselConnectionState.Ready => _strings["StatusConnected"],
            QuasselConnectionState.Error when !string.IsNullOrWhiteSpace(message) => message,
            QuasselConnectionState.Error => _strings["StatusConnectionError"],
            _ => _strings["StatusDisconnected"]
        };
    }

    private void RefreshLocalizedText()
    {
        foreach (var network in Networks)
        {
            network.RefreshLocalizedText(_strings);
        }

        if (!_statusUsesCustomText)
        {
            StatusText = BuildConnectionStatusText(_connectionState, _lastConnectionStateMessage);
        }

        RefreshAppearanceOptions();
        OnPropertyChanged(nameof(ConnectionEndpointText));
        OnPropertyChanged(nameof(ConnectionIdentityText));
        OnPropertyChanged(nameof(TlsModeText));
        OnPropertyChanged(nameof(CurrentSelectionText));
        OnPropertyChanged(nameof(CurrentNetworkText));
        OnPropertyChanged(nameof(SelectedBufferHeadingText));
        OnPropertyChanged(nameof(SelectedBufferSubtitleText));
        OnPropertyChanged(nameof(ShowSelectedBufferSubtitle));
        OnPropertyChanged(nameof(SelectedNetworkStatusText));
        OnPropertyChanged(nameof(SelectedNickText));
        OnPropertyChanged(nameof(SelectedBufferSupportsUserList));
        OnPropertyChanged(nameof(SelectedChannelUsers));
        OnPropertyChanged(nameof(ShowSelectedChannelUsers));
        OnPropertyChanged(nameof(ShowSelectedChannelUsersEmptyState));
        OnPropertyChanged(nameof(ShowSelectedChannelUsersUnavailable));
        OnPropertyChanged(nameof(UserListDisplayMode));
        OnPropertyChanged(nameof(UseOverlayDismissForUserList));
        OnPropertyChanged(nameof(UserListPinButtonText));
        OnPropertyChanged(nameof(UserListStatusText));
        OnPropertyChanged(nameof(ControlPanelNetworkNameText));
        OnPropertyChanged(nameof(ControlPanelServerText));
        OnPropertyChanged(nameof(ControlPanelNickText));
        OnPropertyChanged(nameof(ControlPanelBufferNameText));
        OnPropertyChanged(nameof(ControlPanelBufferPreviewText));
        OnPropertyChanged(nameof(ComposerContextText));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedThemeMode));
        OnPropertyChanged(nameof(ThemeSummaryText));
        OnPropertyChanged(nameof(TrayToolTipText));
        OnPropertyChanged(nameof(ConnectionStatusDetailText));
        OnPropertyChanged(nameof(SessionSummaryText));
        OnPropertyChanged(nameof(SelectedBufferSubtitleText));
        OnPropertyChanged(nameof(ShowSelectedBufferSubtitle));
    }

    private void RaiseSelectionTextPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentSelectionText));
        OnPropertyChanged(nameof(CurrentNetworkText));
        OnPropertyChanged(nameof(SelectedBufferHeadingText));
        OnPropertyChanged(nameof(SelectedBufferSubtitleText));
        OnPropertyChanged(nameof(ShowSelectedBufferSubtitle));
        OnPropertyChanged(nameof(SelectedNetworkStatusText));
        OnPropertyChanged(nameof(SelectedNickText));
        OnPropertyChanged(nameof(SelectedBufferSupportsUserList));
        OnPropertyChanged(nameof(SelectedChannelUsers));
        OnPropertyChanged(nameof(ShowSelectedChannelUsers));
        OnPropertyChanged(nameof(ShowSelectedChannelUsersEmptyState));
        OnPropertyChanged(nameof(ShowSelectedChannelUsersUnavailable));
        OnPropertyChanged(nameof(UserListStatusText));
        OnPropertyChanged(nameof(ControlPanelNetworkNameText));
        OnPropertyChanged(nameof(ControlPanelServerText));
        OnPropertyChanged(nameof(ControlPanelNickText));
        OnPropertyChanged(nameof(ControlPanelBufferNameText));
        OnPropertyChanged(nameof(ControlPanelBufferPreviewText));
        OnPropertyChanged(nameof(ComposerContextText));
        OnPropertyChanged(nameof(TrayToolTipText));
    }

    private void NotifyCommandState()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    public void SetForegroundState(bool isForeground)
    {
        if (_canAcknowledgeSelectedBuffer == isForeground)
        {
            return;
        }

        _canAcknowledgeSelectedBuffer = isForeground;
        if (isForeground)
        {
            AcknowledgeSelectedBuffer();
        }
    }

    private void AcknowledgeSelectedBuffer()
    {
        if (SelectedBuffer is null)
        {
            return;
        }

        SelectedBuffer.MarkRead();
        RaiseSelectionTextPropertiesChanged();
    }

    private void OnBufferPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not BufferItemViewModel buffer)
        {
            return;
        }

        if (ReferenceEquals(buffer, SelectedBuffer)
            && e.PropertyName is nameof(BufferItemViewModel.DisplayName)
                or nameof(BufferItemViewModel.LastMessagePreview)
                or nameof(BufferItemViewModel.ChannelTopic)
                or nameof(BufferItemViewModel.MemberCount)
                or nameof(BufferItemViewModel.UnreadCount)
                or nameof(BufferItemViewModel.HasMentionAlert)
                or nameof(BufferItemViewModel.HasPrivateMessageAlert))
        {
            RaiseSelectionTextPropertiesChanged();
        }

        if (e.PropertyName is nameof(BufferItemViewModel.UnreadCount)
            or nameof(BufferItemViewModel.HasMentionAlert)
            or nameof(BufferItemViewModel.HasPrivateMessageAlert))
        {
            OnPropertyChanged(nameof(SelectedNetworkStatusText));
            OnPropertyChanged(nameof(TrayToolTipText));
        }
    }

    private void DetachFromAllBuffers()
    {
        foreach (var buffer in _buffersById.Values)
        {
            buffer.PropertyChanged -= OnBufferPropertyChanged;
        }
    }

    private void RefreshAppearanceOptions()
    {
        RefreshDisplayOptions(
            _supportedThemes,
            AppThemeCatalog.ThemeKeys,
            key => _strings[AppThemeCatalog.GetThemeDisplayKey(key)]);

        RefreshDisplayOptions(
            _supportedThemeModes,
            AppThemeCatalog.ModeKeys,
            key => _strings[AppThemeCatalog.GetModeDisplayKey(key)]);

        OnPropertyChanged(nameof(SupportedThemes));
        OnPropertyChanged(nameof(SupportedThemeModes));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedThemeMode));
        OnPropertyChanged(nameof(ThemeSummaryText));
    }

    private static void RefreshDisplayOptions(
        ObservableCollection<AppDisplayOption> options,
        IReadOnlyList<string> keys,
        Func<string, string> displayNameSelector)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            var displayName = displayNameSelector(key);

            if (index < options.Count)
            {
                if (!string.Equals(options[index].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    options[index] = new AppDisplayOption(key, displayName);
                }
                else
                {
                    options[index].DisplayName = displayName;
                }
            }
            else
            {
                options.Add(new AppDisplayOption(key, displayName));
            }
        }

        while (options.Count > keys.Count)
        {
            options.RemoveAt(options.Count - 1);
        }
    }

    private void ApplyAppearance()
    {
        App.CurrentApp?.ApplyAppearance(SelectedThemeKey, SelectedThemeModeKey);
    }

    private string BuildPendingAlertSummary()
    {
        var mentionCount = _buffersById.Values.Count(buffer => buffer.HasMentionAlert);
        var privateMessageCount = _buffersById.Values.Count(buffer => buffer.HasPrivateMessageAlert);

        return (mentionCount, privateMessageCount) switch
        {
            (> 0, > 0) => _strings.Format("AlertMentionsAndPrivateMessagesSummary", mentionCount, privateMessageCount),
            (> 0, 0) => _strings.Format("AlertMentionsSummary", mentionCount),
            (0, > 0) => _strings.Format("AlertPrivateMessagesSummary", privateMessageCount),
            _ => string.Empty
        };
    }

    private void RunOnUiThread(Action action)
    {
        if (!_marshalToUiThread || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private void ApplyCachedChannelState(BufferItemViewModel buffer)
    {
        if (buffer.BufferInfo.Type != QuasselBufferType.Channel)
        {
            return;
        }

        var cacheKey = BuildChannelStateKey(buffer.BufferInfo.NetworkId, buffer.BufferInfo.BufferName);
        if (_channelStatesByKey.TryGetValue(cacheKey, out var state))
        {
            buffer.ApplyChannelState(state);
        }
    }

    private void RemoveCachedChannelStates(NetworkId networkId)
    {
        var keysToRemove = _channelStatesByKey.Keys
            .Where(key => key.StartsWith($"{networkId.Value}/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var key in keysToRemove)
        {
            _channelStatesByKey.Remove(key);
        }
    }

    private static string BuildChannelStateKey(NetworkId networkId, string channelName)
    {
        return $"{networkId.Value}/{channelName.Trim()}";
    }

    private static bool IsChannelBufferRemovalMessage(QuasselMessage message)
    {
        return message.BufferInfo.Type == QuasselBufferType.Channel
            && message.IsSelf
            && message.Type.HasFlag(QuasselMessageType.Part);
    }

    private Task SendModeChangeForChannelUserAsync(ChannelUserViewModel? user, string modeChange)
    {
        return SendChannelCommandForUserAsync(user, channelName => $"/mode {channelName} {modeChange} {user!.Nick}");
    }

    private Task SendBufferCommandAsync(
        BufferItemViewModel? buffer,
        QuasselBufferType requiredType,
        Func<QuasselBufferInfo, string> commandFactory)
    {
        if (buffer is null
            || buffer.BufferInfo.Type != requiredType
            || string.IsNullOrWhiteSpace(buffer.BufferInfo.BufferName))
        {
            return Task.CompletedTask;
        }

        var bufferInfo = buffer.BufferInfo;
        return _session.SendInputAsync(bufferInfo, commandFactory(bufferInfo));
    }

    private Task SendChannelCommandForUserAsync(ChannelUserViewModel? user, Func<string, string> commandFactory)
    {
        if (!TryGetSelectedChannelBuffer(out var channelBuffer) || user is null)
        {
            return Task.CompletedTask;
        }

        var bufferInfo = channelBuffer;
        var commandText = commandFactory(bufferInfo.BufferName);
        return _session.SendInputAsync(bufferInfo, commandText);
    }

    private bool TryGetSelectedChannelBuffer([NotNullWhen(true)] out QuasselBufferInfo? channelBuffer)
    {
        if (SelectedBuffer?.BufferInfo.Type == QuasselBufferType.Channel
            && !string.IsNullOrWhiteSpace(SelectedBuffer.BufferInfo.BufferName))
        {
            channelBuffer = SelectedBuffer.BufferInfo;
            return true;
        }

        channelBuffer = null;
        return false;
    }

    private static string BuildNickBanMask(string nick)
    {
        return $"{nick.Trim()}!*@*";
    }
}
