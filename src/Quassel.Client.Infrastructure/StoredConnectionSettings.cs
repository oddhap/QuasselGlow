namespace Quassel.Client.Infrastructure;

public sealed record StoredConnectionSettings(
    string Host = "",
    int Port = 60096,
    string Username = "",
    string Password = "",
    bool TrustInvalidCertificates = false,
    bool RememberLogin = false,
    bool AutoConnectOnStartup = false,
    bool IsControlPanelOpen = false,
    bool IsUserListPinned = false,
    string LanguageCode = "",
    string ThemeKey = "",
    string ThemeModeKey = "",
    bool MinimizeToTray = false,
    bool AutoReconnect = false,
    bool ShowDaySeparators = true,
    // Legacy name: true selects the card-based interface, now called Modern.
    bool UseClassicLayout = true);
