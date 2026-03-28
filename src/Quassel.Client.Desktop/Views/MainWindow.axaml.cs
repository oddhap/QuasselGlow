using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Quassel.Client.Desktop.ViewModels;

namespace Quassel.Client.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private BufferItemViewModel? _observedBuffer;
    private ScrollViewer? _chatScrollHost;
    private bool _isAutoScrolling;
    private bool _stickToBottom = true;
    private int _autoScrollRequestId;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
        AttachToViewModel(DataContext as MainWindowViewModel);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        AttachChatScrollHost();
        UpdateWindowChrome(WindowState);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachToViewModel(DataContext as MainWindowViewModel);
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        DetachFromBuffer();
        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            AttachToBuffer(_viewModel.SelectedBuffer, scrollToBottom: true);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedBuffer))
        {
            AttachToBuffer(_viewModel?.SelectedBuffer, scrollToBottom: true);
        }
    }

    private void AttachToBuffer(BufferItemViewModel? buffer, bool scrollToBottom)
    {
        if (ReferenceEquals(_observedBuffer, buffer))
        {
            if (scrollToBottom)
            {
                QueueScrollToBottom();
            }

            return;
        }

        DetachFromBuffer();
        _observedBuffer = buffer;

        if (_observedBuffer is not null)
        {
            _observedBuffer.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }

        if (scrollToBottom)
        {
            QueueScrollToBottom();
        }
    }

    private void DetachFromBuffer()
    {
        if (_observedBuffer is null)
        {
            return;
        }

        _observedBuffer.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        _observedBuffer = null;
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add
            or NotifyCollectionChangedAction.Move
            or NotifyCollectionChangedAction.Remove
            or NotifyCollectionChangedAction.Replace
            or NotifyCollectionChangedAction.Reset)
        {
            AttachChatScrollHost();
            InvalidateChatLayout();

            if (_stickToBottom || IsNearBottom())
            {
                QueueScrollToBottom();
            }
        }
    }

    private void OnChatScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isAutoScrolling)
        {
            return;
        }

        _stickToBottom = IsNearBottom();
    }

    private void OnChatScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isAutoScrolling)
        {
            return;
        }

        if (_stickToBottom || IsNearBottom())
        {
            QueueScrollToBottom();
        }
    }

    private void QueueScrollToBottom()
    {
        _stickToBottom = true;
        var requestId = ++_autoScrollRequestId;
        Dispatcher.UIThread.Post(async () =>
        {
            _isAutoScrolling = true;

            try
            {
                await ScrollToBottomAfterLayoutAsync(requestId);
            }
            finally
            {
                _isAutoScrolling = false;
                _stickToBottom = true;
            }
        }, DispatcherPriority.Background);
    }

    private bool IsNearBottom()
    {
        if (_chatScrollHost is null)
        {
            return true;
        }

        var remainingHeight = _chatScrollHost.Extent.Height - _chatScrollHost.Viewport.Height - _chatScrollHost.Offset.Y;
        return remainingHeight <= 24;
    }

    private async Task ScrollToBottomAfterLayoutAsync(int requestId)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (requestId != _autoScrollRequestId)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ForceChatLayout();
                ScrollChatToBottomCore();
                ForceChatLayout();
                ScrollChatToBottomCore();
            }, DispatcherPriority.Loaded);

            if (IsNearBottom())
            {
                return;
            }

            await Task.Delay(16);
        }
    }

    private void ScrollChatToBottomCore()
    {
        if (_observedBuffer?.Messages.Count > 0)
        {
            ChatMessagesListBox.ScrollIntoView(_observedBuffer.Messages[^1]);
        }

        if (_chatScrollHost is null)
        {
            return;
        }

        var bottomOffset = Math.Max(0, _chatScrollHost.Extent.Height - _chatScrollHost.Viewport.Height);
        _chatScrollHost.Offset = new Vector(_chatScrollHost.Offset.X, bottomOffset);
    }

    private void ForceChatLayout()
    {
        AttachChatScrollHost();
        ChatMessagesListBox.UpdateLayout();
        _chatScrollHost?.UpdateLayout();
    }

    private void InvalidateChatLayout()
    {
        ChatMessagesListBox.InvalidateMeasure();
        ChatMessagesListBox.InvalidateArrange();
        _chatScrollHost?.InvalidateMeasure();
        _chatScrollHost?.InvalidateArrange();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        DataContextChanged -= OnDataContextChanged;
        Opened -= OnOpened;
        DetachChatScrollHost();
        DetachFromBuffer();

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        if (DataContext is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }

    private void AttachChatScrollHost()
    {
        var scrollHost = ChatMessagesListBox.FindDescendantOfType<ScrollViewer>();
        if (ReferenceEquals(_chatScrollHost, scrollHost))
        {
            return;
        }

        DetachChatScrollHost();
        _chatScrollHost = scrollHost;

        if (_chatScrollHost is not null)
        {
            _chatScrollHost.ScrollChanged += OnChatScrollChanged;
            _chatScrollHost.SizeChanged += OnChatScrollViewerSizeChanged;
        }
    }

    private void DetachChatScrollHost()
    {
        if (_chatScrollHost is null)
        {
            return;
        }

        _chatScrollHost.ScrollChanged -= OnChatScrollChanged;
        _chatScrollHost.SizeChanged -= OnChatScrollViewerSizeChanged;
        _chatScrollHost = null;
    }

    private void OnMinimizeWindowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleMaximizeWindowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseWindowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual sourceVisual
            && sourceVisual.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2 && CanMaximize)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        BeginMoveDrag(e);
        e.Handled = true;
    }

    private void UpdateWindowChrome(WindowState state)
    {
        var isMaximized = state == WindowState.Maximized;
        MaximizeIconCanvas.IsVisible = !isMaximized;
        RestoreIconCanvas.IsVisible = isMaximized;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            UpdateWindowChrome(WindowState);
        }
    }
}
