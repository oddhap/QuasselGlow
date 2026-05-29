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
    bool AutoReconnect = false);
