using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using QuasselGlow.Appearance;
using QuasselGlow.ViewModels;
using QuasselGlow.Views;

namespace QuasselGlow;

public partial class App : Avalonia.Application
{
    private readonly WallpaperPaletteProvider _wallpaperPaletteProvider = new();
    private MainWindowViewModel? _mainWindowViewModel;
    private MainWindowBase? _mainWindow;
    private bool _isSwitchingLayout;

    public static App? CurrentApp => Application.Current as App;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindowViewModel = new MainWindowViewModel();
            ApplyAppearance(_mainWindowViewModel.SelectedThemeKey, _mainWindowViewModel.SelectedThemeModeKey);
            _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;

            _mainWindow = CreateMainWindow(_mainWindowViewModel.UseClassicLayout);
            _mainWindow.DataContext = _mainWindowViewModel;
            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindowBase CreateMainWindow(bool useClassicLayout)
    {
        return useClassicLayout ? new ClassicMainWindow() : new MainWindow();
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.UseClassicLayout))
        {
            return;
        }

        Dispatcher.UIThread.Post(SwitchLayoutIfNeeded, DispatcherPriority.Background);
    }

    private void SwitchLayoutIfNeeded()
    {
        if (_isSwitchingLayout
            || _mainWindowViewModel is null
            || ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var useClassicLayout = _mainWindowViewModel.UseClassicLayout;
        var expectedType = useClassicLayout ? typeof(ClassicMainWindow) : typeof(MainWindow);
        if (_mainWindow is not null && _mainWindow.GetType() == expectedType)
        {
            return;
        }

        _isSwitchingLayout = true;
        try
        {
            var oldWindow = _mainWindow;
            var newWindow = CreateMainWindow(useClassicLayout);
            newWindow.DataContext = _mainWindowViewModel;

            if (oldWindow is not null)
            {
                newWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                newWindow.Width = oldWindow.Width;
                newWindow.Height = oldWindow.Height;
            }

            _mainWindow = newWindow;
            desktop.MainWindow = newWindow;
            newWindow.Show();

            if (oldWindow is not null)
            {
                newWindow.Position = oldWindow.Position;

                if (oldWindow.WindowState == WindowState.Maximized)
                {
                    newWindow.WindowState = WindowState.Maximized;
                }

                oldWindow.SuppressViewModelDispose = true;
                oldWindow.Close();
            }
        }
        finally
        {
            _isSwitchingLayout = false;
        }
    }

    public void ApplyAppearance(string? themeKey, string? modeKey)
    {
        var wallpaperColors = AppThemeCatalog.IsWallpaperMatchedTheme(themeKey)
            ? _wallpaperPaletteProvider.TryGetThemeColors()
            : null;
        var palette = AppThemeCatalog.ResolvePalette(themeKey, modeKey, wallpaperColors);
        RequestedThemeVariant = AppThemeCatalog.IsDarkMode(modeKey) ? ThemeVariant.Dark : ThemeVariant.Light;

        UpdateBrush("ShellBg", palette.ShellBg);
        UpdateBrush("ShellPanel", palette.ShellPanel);
        UpdateBrush("ShellPanelMuted", palette.ShellPanelMuted);
        UpdateBrush("ShellBorder", palette.ShellBorder);
        UpdateBrush("InkStrong", palette.InkStrong);
        UpdateBrush("InkSoft", palette.InkSoft);
        UpdateBrush("AccentRust", palette.AccentRust);
        UpdateBrush("AccentTeal", palette.AccentTeal);
        UpdateBrush("AccentSky", palette.AccentSky);
        UpdateBrush("AccentSand", palette.AccentSand);
        UpdateBrush("MessageRowBg", palette.MessageRowBg);
        UpdateBrush("MessageRowBorder", palette.MessageRowBorder);
        UpdateBrush("MessageRowSelfBg", palette.MessageRowSelfBg);
        UpdateBrush("MessageRowSelfBorder", palette.MessageRowSelfBorder);
        UpdateBrush("MessageRowHighlightBg", palette.MessageRowHighlightBg);
        UpdateBrush("MessageRowHighlightBorder", palette.MessageRowHighlightBorder);
        UpdateBrush("MessageRowStatusBg", palette.MessageRowStatusBg);
        UpdateBrush("ComposerSelection", palette.ComposerSelection);
        UpdateBrush("WindowChromeHoverBg", palette.WindowChromeHoverBg);
        UpdateBrush("WindowChromePressedBg", palette.WindowChromePressedBg);
        UpdateBrush("WindowChromeCloseFg", palette.WindowChromeCloseFg);
        UpdateBrush("WindowChromeCloseHoverBg", palette.WindowChromeCloseHoverBg);
        UpdateBrush("WindowChromeCloseHoverFg", palette.WindowChromeCloseHoverFg);
        UpdateBrush("WindowChromeClosePressedBg", palette.WindowChromeClosePressedBg);
        UpdateBrush("WindowChromeClosePressedFg", palette.WindowChromeClosePressedFg);
        UpdateBrush("BufferPrefixBg", palette.BufferPrefixBg);
        UpdateBrush("MenuFlyoutPresenterBackground", palette.ShellPanel);
        UpdateBrush("MenuFlyoutPresenterBorderBrush", palette.ShellBorder);
        UpdateBrush("MenuFlyoutItemBackground", palette.ShellPanel);
        UpdateBrush("MenuFlyoutItemForeground", palette.InkStrong);
        UpdateBrush("MenuFlyoutItemBackgroundPointerOver", palette.AccentSky);
        UpdateBrush("MenuFlyoutItemForegroundPointerOver", palette.AccentTeal);
        UpdateBrush("MenuFlyoutItemBackgroundPressed", palette.WindowChromePressedBg);
        UpdateBrush("MenuFlyoutItemForegroundPressed", palette.AccentTeal);
        UpdateBrush("MenuFlyoutItemBackgroundDisabled", palette.ShellPanelMuted);
        UpdateBrush("MenuFlyoutItemForegroundDisabled", palette.InkSoft);
        UpdateBrush("MenuFlyoutItemKeyboardAcceleratorTextForeground", palette.InkSoft);
        UpdateBrush("MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", palette.AccentTeal);
        UpdateBrush("MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", palette.AccentTeal);
        UpdateBrush("MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", palette.InkSoft);
        UpdateBrush("MenuFlyoutSubItemChevron", palette.InkSoft);
        UpdateBrush("MenuFlyoutSubItemChevronPointerOver", palette.AccentTeal);
        UpdateBrush("MenuFlyoutSubItemChevronPressed", palette.AccentTeal);
        UpdateBrush("MenuFlyoutSubItemChevronDisabled", palette.InkSoft);
        UpdateBrush("MenuFlyoutSubItemChevronSubMenuOpened", palette.AccentTeal);
        UpdateBrush("ContextMenuSeparatorBrush", palette.ShellBorder);
        UpdateBackdropBrush(palette);
    }

    private void UpdateBrush(string key, Color color)
    {
        if (Resources[key] is SolidColorBrush existing)
        {
            existing.Color = color;
            return;
        }

        Resources[key] = new SolidColorBrush(color);
    }

    private void UpdateBackdropBrush(AppThemePalette palette)
    {
        if (Resources["ShellBackdropBrush"] is LinearGradientBrush existing
            && existing.GradientStops.Count >= 3)
        {
            existing.GradientStops[0].Color = palette.ShellBackdropStart;
            existing.GradientStops[1].Color = palette.ShellBackdropMid;
            existing.GradientStops[2].Color = palette.ShellBackdropEnd;
            return;
        }

        Resources["ShellBackdropBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(palette.ShellBackdropStart, 0),
                new GradientStop(palette.ShellBackdropMid, 0.55),
                new GradientStop(palette.ShellBackdropEnd, 1)
            ]
        };
    }
}
