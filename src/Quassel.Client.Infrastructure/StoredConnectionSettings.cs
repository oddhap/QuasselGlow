namespace Quassel.Client.Infrastructure;

public sealed record StoredConnectionSettings(
    string Host = "",
    int Port = 60096,
    string Username = "",
    string Password = "",
    bool TrustInvalidCertificates = false,
    bool RememberLogin = false,
    bool IsControlPanelOpen = false,
    string LanguageCode = "");
