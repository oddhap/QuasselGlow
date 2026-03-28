using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quassel.Client.Desktop.Localization;
using Quassel.Client.Domain;
using Quassel.Client.Infrastructure;

namespace Quassel.Client.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly QuasselSessionService _session = new();
    private readonly LocalConnectionSettingsStore _settingsStore = new();
    private readonly UiTextCatalog _strings = UiTextCatalog.Instance;
    private readonly Dictionary<NetworkId, NetworkItemViewModel> _networksById = new();
    private readonly Dictionary<BufferId, BufferItemViewModel> _buffersById = new();

    private QuasselConnectionState _connectionState = QuasselConnectionState.Disconnected;
    private BufferItemViewModel? _selectedBuffer;
    private bool _isApplyingStoredSettings;
    private bool _statusUsesCustomText;
    private string? _lastConnectionStateMessage;
    private string _selectedLanguageCode = UiTextCatalog.Instance.CurrentLanguageCode;

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
    private bool _isControlPanelOpen;

    [ObservableProperty]
    private string _statusText = UiTextCatalog.Instance["StatusDisconnected"];

    [ObservableProperty]
    private string _draftMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public MainWindowViewModel()
    {
        _isApplyingStoredSettings = true;
        try
        {
            ApplyStoredSettings(_settingsStore.Load());
        }
        finally
        {
            _isApplyingStoredSettings = false;
        }

        StatusText = BuildConnectionStatusText(_connectionState, _lastConnectionStateMessage);

        _session.ConnectionStateChanged += OnConnectionStateChanged;
        _session.SessionStateReceived += OnSessionStateReceived;
        _session.NetworkStateReceived += state => Dispatcher.UIThread.Post(() => UpsertNetwork(state));
        _session.BufferInfoUpdated += info => Dispatcher.UIThread.Post(() => UpsertBuffer(info));
        _session.MessageReceived += message => Dispatcher.UIThread.Post(() => ApplyMessage(message));
        _session.StatusReceived += message => Dispatcher.UIThread.Post(() => ApplyExternalStatus(message));
        _session.NetworkRemoved += networkId => Dispatcher.UIThread.Post(() => RemoveNetwork(networkId));
    }

    public UiTextCatalog Strings => _strings;

    public IReadOnlyList<UiLanguageOption> SupportedLanguages => _strings.SupportedLanguages;

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
                    _selectedBuffer.MarkRead();
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

    public string CurrentSelectionText => SelectedBuffer?.DisplayName ?? _strings["SelectBufferToStart"];

    public string CurrentNetworkText => SelectedNetwork?.DisplayName ?? _strings["NoNetworkSelected"];

    public string SelectedBufferHeadingText => SelectedBuffer?.DisplayName ?? _strings["NoBufferSelected"];

    public string SelectedNetworkStatusText => SelectedNetwork?.StatusText ?? _strings["SelectChannelOrQuery"];

    public string SelectedNickText => string.IsNullOrWhiteSpace(SelectedNetwork?.MyNick) ? "nick" : SelectedNetwork.MyNick;

    public string ControlPanelNetworkNameText => SelectedNetwork?.DisplayName ?? _strings["NoneSelected"];

    public string ControlPanelServerText =>
        string.IsNullOrWhiteSpace(SelectedNetwork?.CurrentServer) ? _strings["ServerNotLoaded"] : SelectedNetwork.CurrentServer;

    public string ControlPanelNickText => string.IsNullOrWhiteSpace(SelectedNetwork?.MyNick) ? _strings["NickNotKnown"] : SelectedNetwork.MyNick;

    public string ControlPanelBufferNameText => SelectedBuffer?.DisplayName ?? _strings["SelectBuffer"];

    public string ControlPanelBufferPreviewText =>
        string.IsNullOrWhiteSpace(SelectedBuffer?.LastMessagePreview) ? _strings["NoMessagesYet"] : SelectedBuffer.LastMessagePreview;

    public string ComposerContextText => SelectedBuffer?.DisplayName ?? _strings["SelectChannelOrQuery"];

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
    private void ToggleConnectionEditor()
    {
        IsConnectionEditorOpen = !IsConnectionEditorOpen;
    }

    [RelayCommand]
    private void CloseConnectionEditor()
    {
        IsConnectionEditorOpen = false;
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

    partial void OnTrustInvalidCertificatesChanged(bool value)
    {
        SaveSettingsIfReady();
        OnPropertyChanged(nameof(TlsModeText));
    }

    partial void OnIsControlPanelOpenChanged(bool value)
    {
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
        SaveSettingsIfReady();
    }

    partial void OnPortTextChanged(string value)
    {
        OnPropertyChanged(nameof(ConnectionEndpointText));
        SaveSettingsIfReady();
    }

    partial void OnUsernameChanged(string value)
    {
        OnPropertyChanged(nameof(ConnectionIdentityText));
        SaveSettingsIfReady();
    }

    partial void OnPasswordChanged(string value)
    {
        SaveSettingsIfReady();
    }

    private void OnConnectionStateChanged(QuasselConnectionState state, string? message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _connectionState = state;
            _lastConnectionStateMessage = message;
            _statusUsesCustomText = state == QuasselConnectionState.Error && !string.IsNullOrWhiteSpace(message);
            StatusText = _statusUsesCustomText
                ? message!
                : BuildConnectionStatusText(state, message);

            OnPropertyChanged(nameof(ConnectionBrush));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ShowConnectAction));
            OnPropertyChanged(nameof(ShowDisconnectAction));
            NotifyCommandState();

            if (state is QuasselConnectionState.Disconnected or QuasselConnectionState.Error)
            {
                Networks.Clear();
                _networksById.Clear();
                _buffersById.Clear();
                SelectedBuffer = null;
            }
        });
    }

    private void ApplyExternalStatus(string message)
    {
        _statusUsesCustomText = true;
        StatusText = message;
    }

    private void OnSessionStateReceived(QuasselSessionState sessionState)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Networks.Clear();
            _networksById.Clear();
            _buffersById.Clear();

            foreach (var networkId in sessionState.Networks)
            {
                EnsureNetwork(networkId);
            }

            foreach (var buffer in sessionState.Buffers)
            {
                UpsertBuffer(buffer);
            }

            SelectedBuffer ??= PickInitialBuffer();
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
        return created;
    }

    private void UpsertBuffer(QuasselBufferInfo bufferInfo)
    {
        var network = EnsureNetwork(bufferInfo.NetworkId);
        if (_buffersById.TryGetValue(bufferInfo.BufferId, out var existing))
        {
            existing.UpdateInfo(bufferInfo);
            if (SelectedBuffer?.BufferInfo.BufferId == bufferInfo.BufferId)
            {
                RaiseSelectionTextPropertiesChanged();
            }

            return;
        }

        var created = new BufferItemViewModel(bufferInfo);
        _buffersById[bufferInfo.BufferId] = created;
        network.UpsertBuffer(created);

        SelectedBuffer ??= PickInitialBuffer();
    }

    private void ApplyMessage(QuasselMessage message)
    {
        if (!_buffersById.TryGetValue(message.BufferInfo.BufferId, out var buffer))
        {
            UpsertBuffer(message.BufferInfo);
            buffer = _buffersById[message.BufferInfo.BufferId];
        }

        buffer.AddMessage(message);
        if (SelectedBuffer?.BufferInfo.BufferId == buffer.BufferInfo.BufferId)
        {
            buffer.MarkRead();
            RaiseSelectionTextPropertiesChanged();
        }
    }

    private void RemoveNetwork(NetworkId networkId)
    {
        if (!_networksById.TryGetValue(networkId, out var network))
        {
            return;
        }

        foreach (var buffer in network.Buffers)
        {
            _buffersById.Remove(buffer.BufferInfo.BufferId);
        }

        Networks.Remove(network);
        _networksById.Remove(networkId);

        if (SelectedBuffer is not null && SelectedBuffer.BufferInfo.NetworkId == networkId)
        {
            SelectedBuffer = PickInitialBuffer();
        }

        RaiseSelectionTextPropertiesChanged();
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
                _statusUsesCustomText = true;
                StatusText = ex.Message;
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
            SelectedLanguageCode));
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

        OnPropertyChanged(nameof(ConnectionEndpointText));
        OnPropertyChanged(nameof(ConnectionIdentityText));
        OnPropertyChanged(nameof(TlsModeText));
        OnPropertyChanged(nameof(CurrentSelectionText));
        OnPropertyChanged(nameof(CurrentNetworkText));
        OnPropertyChanged(nameof(SelectedBufferHeadingText));
        OnPropertyChanged(nameof(SelectedNetworkStatusText));
        OnPropertyChanged(nameof(SelectedNickText));
        OnPropertyChanged(nameof(ControlPanelNetworkNameText));
        OnPropertyChanged(nameof(ControlPanelServerText));
        OnPropertyChanged(nameof(ControlPanelNickText));
        OnPropertyChanged(nameof(ControlPanelBufferNameText));
        OnPropertyChanged(nameof(ControlPanelBufferPreviewText));
        OnPropertyChanged(nameof(ComposerContextText));
        OnPropertyChanged(nameof(SelectedLanguage));
    }

    private void RaiseSelectionTextPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentSelectionText));
        OnPropertyChanged(nameof(CurrentNetworkText));
        OnPropertyChanged(nameof(SelectedBufferHeadingText));
        OnPropertyChanged(nameof(SelectedNetworkStatusText));
        OnPropertyChanged(nameof(SelectedNickText));
        OnPropertyChanged(nameof(ControlPanelNetworkNameText));
        OnPropertyChanged(nameof(ControlPanelServerText));
        OnPropertyChanged(nameof(ControlPanelNickText));
        OnPropertyChanged(nameof(ControlPanelBufferNameText));
        OnPropertyChanged(nameof(ControlPanelBufferPreviewText));
        OnPropertyChanged(nameof(ComposerContextText));
    }

    private void NotifyCommandState()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        SendMessageCommand.NotifyCanExecuteChanged();
    }
}
