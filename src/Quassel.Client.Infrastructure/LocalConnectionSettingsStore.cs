using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Quassel.Client.Infrastructure;

public sealed class LocalConnectionSettingsStore : IConnectionSettingsStore
{
    private const string ProtectedPrefix = "dpapi:";
    private const string PlainPrefix = "plain:";
    private static readonly byte[] CurrentOptionalEntropy = Encoding.UTF8.GetBytes("QuasselGlow.ConnectionSettings.v1");
    private static readonly byte[] LegacyOptionalEntropy = Encoding.UTF8.GetBytes("QuasselNeon.ConnectionSettings.v1");
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;
    private readonly string? _legacySettingsPath;

    public LocalConnectionSettingsStore(string? settingsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            _settingsPath = settingsPath;
            _legacySettingsPath = null;
            return;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(localAppData, "QuasselGlow", "settings.json");
        _legacySettingsPath = Path.Combine(localAppData, "QuasselNeon", "settings.json");
    }

    public ConnectionSettingsLoadResult Load()
    {
        var sourcePath = ResolveExistingSettingsPath();
        if (sourcePath is null)
        {
            return ConnectionSettingsLoadResult.Missing();
        }

        try
        {
            var json = File.ReadAllText(sourcePath);
            var persisted = JsonSerializer.Deserialize<PersistedConnectionSettings>(json, SerializerOptions)
                ?? new PersistedConnectionSettings();

            var password = string.Empty;
            if (persisted.RememberLogin && !string.IsNullOrWhiteSpace(persisted.Password))
            {
                try
                {
                    password = Unprotect(persisted.Password);
                }
                catch
                {
                    password = string.Empty;
                }
            }

            var settings = new StoredConnectionSettings(
                string.IsNullOrWhiteSpace(persisted.Host) ? string.Empty : persisted.Host.Trim(),
                persisted.Port > 0 ? persisted.Port : 60096,
                persisted.RememberLogin ? persisted.Username ?? string.Empty : string.Empty,
                persisted.RememberLogin ? password : string.Empty,
                persisted.TrustInvalidCertificates,
                persisted.RememberLogin,
                persisted.RememberLogin && persisted.AutoConnectOnStartup,
                persisted.IsControlPanelOpen,
                persisted.IsUserListPinned,
                string.IsNullOrWhiteSpace(persisted.LanguageCode) ? string.Empty : persisted.LanguageCode.Trim(),
                string.IsNullOrWhiteSpace(persisted.ThemeKey) ? string.Empty : persisted.ThemeKey.Trim(),
                string.IsNullOrWhiteSpace(persisted.ThemeModeKey) ? string.Empty : persisted.ThemeModeKey.Trim(),
                persisted.MinimizeToTray);

            return ConnectionSettingsLoadResult.Loaded(settings);
        }
        catch (Exception ex)
        {
            return ConnectionSettingsLoadResult.Failed(ex.Message);
        }
    }

    public ConnectionSettingsSaveResult Save(StoredConnectionSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var protectionResult = settings.RememberLogin && !string.IsNullOrEmpty(settings.Password)
                ? Protect(settings.Password)
                : ProtectedSecret.Empty;

            var persisted = new PersistedConnectionSettings
            {
                Host = string.IsNullOrWhiteSpace(settings.Host) ? string.Empty : settings.Host.Trim(),
                Port = settings.Port > 0 ? settings.Port : 60096,
                Username = settings.RememberLogin ? settings.Username.Trim() : string.Empty,
                Password = protectionResult.Value,
                TrustInvalidCertificates = settings.TrustInvalidCertificates,
                RememberLogin = settings.RememberLogin,
                AutoConnectOnStartup = settings.RememberLogin && settings.AutoConnectOnStartup,
                IsControlPanelOpen = settings.IsControlPanelOpen,
                IsUserListPinned = settings.IsUserListPinned,
                LanguageCode = string.IsNullOrWhiteSpace(settings.LanguageCode) ? string.Empty : settings.LanguageCode.Trim(),
                ThemeKey = string.IsNullOrWhiteSpace(settings.ThemeKey) ? string.Empty : settings.ThemeKey.Trim(),
                ThemeModeKey = string.IsNullOrWhiteSpace(settings.ThemeModeKey) ? string.Empty : settings.ThemeModeKey.Trim(),
                MinimizeToTray = settings.MinimizeToTray
            };

            var json = JsonSerializer.Serialize(persisted, SerializerOptions);
            File.WriteAllText(_settingsPath, json);

            return protectionResult.IsDegraded
                ? ConnectionSettingsSaveResult.SavedWithDegradedCredentialProtection()
                : ConnectionSettingsSaveResult.Saved();
        }
        catch (Exception ex)
        {
            return ConnectionSettingsSaveResult.Failed(ex.Message);
        }
    }

    private string? ResolveExistingSettingsPath()
    {
        if (File.Exists(_settingsPath))
        {
            return _settingsPath;
        }

        if (!string.IsNullOrWhiteSpace(_legacySettingsPath) && File.Exists(_legacySettingsPath))
        {
            return _legacySettingsPath;
        }

        return null;
    }

    private static ProtectedSecret Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ProtectedSecret.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(value);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var protectedBytes = ProtectedData.Protect(plainBytes, CurrentOptionalEntropy, DataProtectionScope.CurrentUser);
                return new ProtectedSecret(ProtectedPrefix + Convert.ToBase64String(protectedBytes), IsDegraded: false);
            }
        }
        catch
        {
        }

        return new ProtectedSecret(PlainPrefix + Convert.ToBase64String(plainBytes), IsDegraded: true);
    }

    private static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                return string.Empty;
            }

            var protectedBytes = Convert.FromBase64String(value[ProtectedPrefix.Length..]);
            var plainBytes = TryUnprotectWindows(protectedBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }

        if (value.StartsWith(PlainPrefix, StringComparison.Ordinal))
        {
            var plainBytes = Convert.FromBase64String(value[PlainPrefix.Length..]);
            return Encoding.UTF8.GetString(plainBytes);
        }

        return string.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static byte[] TryUnprotectWindows(byte[] protectedBytes)
    {
        try
        {
            return ProtectedData.Unprotect(protectedBytes, CurrentOptionalEntropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return ProtectedData.Unprotect(protectedBytes, LegacyOptionalEntropy, DataProtectionScope.CurrentUser);
        }
    }

    private sealed class PersistedConnectionSettings
    {
        public string Host { get; init; } = string.Empty;
        public int Port { get; init; } = 60096;
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public bool TrustInvalidCertificates { get; init; }
        public bool RememberLogin { get; init; }
        public bool AutoConnectOnStartup { get; init; }
        public bool IsControlPanelOpen { get; init; }
        public bool IsUserListPinned { get; init; }
        public string LanguageCode { get; init; } = string.Empty;
        public string ThemeKey { get; init; } = string.Empty;
        public string ThemeModeKey { get; init; } = string.Empty;
        public bool MinimizeToTray { get; init; }
    }

    private sealed record ProtectedSecret(string Value, bool IsDegraded)
    {
        public static ProtectedSecret Empty { get; } = new(string.Empty, IsDegraded: false);
    }
}
