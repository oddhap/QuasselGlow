using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using QuasselGlow.ViewModels;

namespace QuasselGlow.Views;

public partial class DccChatWindow : Window
{
    private DccChatViewModel? _viewModel;
    private bool _stickToBottom = true;
    private bool _closed;

    public DccChatWindow()
    {
        InitializeComponent();
        OutputScrollViewer.ScrollChanged += (_, e) =>
        {
            if (e.ExtentDelta.Y == 0)
                _stickToBottom = OutputScrollViewer.Extent.Height - OutputScrollViewer.Viewport.Height - OutputScrollViewer.Offset.Y < 24;
        };
        Opened += async (_, _) =>
        {
            _viewModel = DataContext as DccChatViewModel;
            if (_viewModel is null) return;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            await _viewModel.StartAsync();
        };
        Closed += async (_, _) =>
        {
            _closed = true;
            if (_viewModel is null) return;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            await _viewModel.DisposeAsync();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DccChatViewModel.Output) && _stickToBottom)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_closed) return;
                OutputScrollViewer.ScrollToEnd();
            }, DispatcherPriority.Loaded);
        }
        else if (e.PropertyName == nameof(DccChatViewModel.IsConnected) && _viewModel?.IsConnected == true)
        {
            GameComposer.Focus();
        }
    }

    private async void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || _viewModel is null) return;
        if (e.Key == Key.Enter && _viewModel.SendCommand.CanExecute(null))
        {
            e.Handled = true;
            await _viewModel.SendCommand.ExecuteAsync(null);
        }
        else if (e.Key is Key.Up or Key.Down && _viewModel.RecallHistory(e.Key == Key.Up))
        {
            e.Handled = true;
            GameComposer.CaretIndex = _viewModel.Draft.Length;
        }
    }
}
