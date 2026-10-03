using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quassel.Client.Application.Dcc;
using Quassel.Client.Domain;
using Quassel.Client.Infrastructure;
using QuasselGlow.Localization;

namespace QuasselGlow.ViewModels;

public sealed partial class DccChatViewModel : ViewModelBase, IAsyncDisposable
{
    private const int MaxOutputLength = 200_000;
    private readonly IDccChatSession _session;
    private readonly bool _marshalToUiThread;
    private readonly DccTextCleaner _cleaner = new();
    private readonly List<string> _history = [];
    private readonly List<MessageTextSegment> _outputSegments = [];
    public IReadOnlyList<MessageTextSegment> OutputSegments { get; private set; } = [];
    private int _historyIndex;
    private string _savedDraft = string.Empty;
    private DccChatState _state;
    private string? _error;
    private bool _started;
    private bool _disposed;

    [ObservableProperty]
    private string _output = string.Empty;

    [ObservableProperty]
    private string _draft = string.Empty;

    public DccChatViewModel(DccChatOffer offer, IDccChatSession? session = null, bool marshalToUiThread = true)
    {
        Offer = offer;
        _session = session ?? new DccChatSession();
        _marshalToUiThread = marshalToUiThread;
        _session.TextReceived += OnTextReceived;
        _session.StateChanged += OnStateChanged;
        Strings.LanguageChanged += OnLanguageChanged;
    }

    public DccChatOffer Offer { get; }
    public UiTextCatalog Strings => UiTextCatalog.Instance;
    public string Title => $"DCC · {Offer.Nick}";
    public string Endpoint => Offer.Endpoint;
    public bool IsConnected => _state == DccChatState.Connected && !_disposed;
    public bool CanDisconnect => _state is DccChatState.Connecting or DccChatState.Connected;
    public string StatusText => _state switch
    {
        DccChatState.Connecting => Strings["DccConnecting"],
        DccChatState.Connected => Strings["DccConnected"],
        DccChatState.Error => Strings.Format("DccFailed", _error),
        _ => Strings["DccDisconnected"]
    };

    public async Task StartAsync()
    {
        if (_started || _disposed) return;
        _started = true;
        await _session.ConnectAsync(Offer);
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task SendAsync()
    {
        var text = Draft;
        try
        {
            // Slash commands and blank lines belong to the game, never to the IRC core.
            await _session.SendLineAsync(text);
            RunOnUiThread(() =>
            {
                var sensitive = IsSensitiveCommand(text);
                var echo = sensitive ? text.TrimStart().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0] + " ••••" : text;
                AppendOutput($"{(Output.EndsWith('\n') || Output.Length == 0 ? "" : "\n")}> {echo}\n");
                if (Draft == text) Draft = string.Empty;
                if (!sensitive && text.Length > 0 && (_history.Count == 0 || _history[^1] != text)) _history.Add(text);
                if (_history.Count > 100) _history.RemoveAt(0);
                _historyIndex = _history.Count;
                _savedDraft = string.Empty;
            });
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException
                                  or OperationCanceledException or InvalidOperationException or ArgumentException)
        {
            RunOnUiThread(() =>
            {
                _error = ex.Message;
                AppendOutput(Strings.Format("DccSendFailed", ex.Message) + "\n");
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private Task DisconnectAsync() => _session.DisconnectAsync();

    public bool RecallHistory(bool previous)
    {
        if (_history.Count == 0) return false;
        if (previous)
        {
            if (_historyIndex == 0) return false;
            if (_historyIndex == _history.Count) _savedDraft = IsSensitiveCommand(Draft) ? string.Empty : Draft;
            Draft = _history[--_historyIndex];
        }
        else
        {
            if (_historyIndex >= _history.Count) return false;
            _historyIndex++;
            Draft = _historyIndex == _history.Count ? _savedDraft : _history[_historyIndex];
        }
        return true;
    }

    private static bool IsSensitiveCommand(string text)
    {
        var command = text.TrimStart().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return command is not null && (command.Equals("!login", StringComparison.OrdinalIgnoreCase)
            || command.Equals("/login", StringComparison.OrdinalIgnoreCase));
    }

    private void OnTextReceived(string text) => RunOnUiThread(() =>
    {
        var runs = _cleaner.CleanRuns(text);
        AppendOutput(string.Concat(runs.Select(run => run.Text)), MessageTextSegment.FromRuns(runs));
    });

    private void OnStateChanged(DccChatState state, string? detail) => RunOnUiThread(() =>
    {
        _state = state;
        _error = detail;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(CanDisconnect));
        SendCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    });

    private void OnLanguageChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(StatusText));

    private void AppendOutput(string text, IEnumerable<MessageTextSegment>? segments = null)
    {
        if (text.Length == 0) return;
        _outputSegments.AddRange(segments ?? [new MessageTextSegment(text)]);
        var output = Output + text;
        if (output.Length > MaxOutputLength)
        {
            var start = output.Length - MaxOutputLength;
            // Keep complete lines when truncating the scrollback where possible.
            var nextLine = output.IndexOf('\n', start);
            var trim = nextLine >= 0 ? nextLine + 1 : start;
            output = output[trim..];
            while (trim > 0 && _outputSegments.Count > 0)
            {
                var first = _outputSegments[0];
                if (first.Text.Length <= trim) { trim -= first.Text.Length; _outputSegments.RemoveAt(0); }
                else { _outputSegments[0] = new(first.Text[trim..], first.Url, first.Style); trim = 0; }
            }
        }
        OutputSegments = _outputSegments.ToArray();
        OnPropertyChanged(nameof(OutputSegments));
        Output = output;
    }

    private void RunOnUiThread(Action action)
    {
        if (_disposed) return;
        if (!_marshalToUiThread || Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(() => { if (!_disposed) action(); });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _session.TextReceived -= OnTextReceived;
        _session.StateChanged -= OnStateChanged;
        Strings.LanguageChanged -= OnLanguageChanged;
        await _session.DisposeAsync();
    }
}
