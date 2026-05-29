using Quassel.Client.Infrastructure;

namespace Quassel.Client.Application.Tests;

public sealed class LocalConnectionSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "quassel-settings-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsRememberedLogin()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = new LocalConnectionSettingsStore(path);

        var expected = new StoredConnectionSettings(
            Host: "chat.example",
            Port: 60096,
            Username: "alice",
            Password: "hemmelig",
            TrustInvalidCertificates: true,
            RememberLogin: true,
            AutoConnectOnStartup: true,
            IsControlPanelOpen: true,
            IsUserListPinned: true,
            LanguageCode: "nb",
            ThemeKey: "ember",
            ThemeModeKey: "dark",
            MinimizeToTray: true,
            AutoReconnect: true);

        var saveResult = store.Save(expected);
        var loadResult = store.Load();
        var actual = loadResult.Settings;

        Assert.NotEqual(ConnectionSettingsSaveStatus.Failed, saveResult.Status);
        Assert.Equal(ConnectionSettingsLoadStatus.Loaded, loadResult.Status);
        Assert.Equal(expected.Host, actual.Host);
        Assert.Equal(expected.Port, actual.Port);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.Password, actual.Password);
        Assert.Equal(expected.TrustInvalidCertificates, actual.TrustInvalidCertificates);
        Assert.True(actual.RememberLogin);
        Assert.True(actual.AutoConnectOnStartup);
        Assert.True(actual.IsControlPanelOpen);
        Assert.True(actual.IsUserListPinned);
        Assert.Equal(expected.LanguageCode, actual.LanguageCode);
        Assert.Equal(expected.ThemeKey, actual.ThemeKey);
        Assert.Equal(expected.ThemeModeKey, actual.ThemeModeKey);
        Assert.True(actual.MinimizeToTray);
        Assert.True(actual.AutoReconnect);
    }

    [Fact]
    public void SaveAndLoad_ClearsCredentialsWhenRememberLoginIsDisabled()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = new LocalConnectionSettingsStore(path);

        var saveResult = store.Save(new StoredConnectionSettings(
            Host: "server.example",
            Port: 4242,
            Username: "bruker",
            Password: "passord",
            TrustInvalidCertificates: true,
            RememberLogin: false,
            AutoConnectOnStartup: true,
            IsControlPanelOpen: false,
            IsUserListPinned: false,
            LanguageCode: "en_US",
            ThemeKey: "tide",
            ThemeModeKey: "light",
            MinimizeToTray: false,
            AutoReconnect: true));

        var loadResult = store.Load();
        var actual = loadResult.Settings;

        Assert.Equal(ConnectionSettingsSaveStatus.Saved, saveResult.Status);
        Assert.Equal(ConnectionSettingsLoadStatus.Loaded, loadResult.Status);
        Assert.Equal("server.example", actual.Host);
        Assert.Equal(4242, actual.Port);
        Assert.True(actual.TrustInvalidCertificates);
        Assert.False(actual.RememberLogin);
        Assert.False(actual.AutoConnectOnStartup);
        Assert.False(actual.IsControlPanelOpen);
        Assert.False(actual.IsUserListPinned);
        Assert.Equal("en_US", actual.LanguageCode);
        Assert.Equal("tide", actual.ThemeKey);
        Assert.Equal("light", actual.ThemeModeKey);
        Assert.False(actual.MinimizeToTray);
        Assert.True(actual.AutoReconnect);
        Assert.Equal(string.Empty, actual.Username);
        Assert.Equal(string.Empty, actual.Password);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaultSettingsWithFailedStatus()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(path, "{not json");
        var store = new LocalConnectionSettingsStore(path);

        var result = store.Load();

        Assert.Equal(ConnectionSettingsLoadStatus.Failed, result.Status);
        Assert.Equal(new StoredConnectionSettings(), result.Settings);
    }

    [Fact]
    public void Save_UnwritablePath_ReturnsFailedStatus()
    {
        Directory.CreateDirectory(_tempDirectory);
        var blockerPath = Path.Combine(_tempDirectory, "blocker");
        File.WriteAllText(blockerPath, "not a directory");
        var path = Path.Combine(blockerPath, "settings.json");
        var store = new LocalConnectionSettingsStore(path);

        var result = store.Save(new StoredConnectionSettings(Host: "chat.example"));

        Assert.Equal(ConnectionSettingsSaveStatus.Failed, result.Status);
    }

    [Fact]
    public void Save_RememberedPasswordWithoutWindowsProtection_ReturnsDegradedCredentialStatus()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = new LocalConnectionSettingsStore(path);

        var result = store.Save(new StoredConnectionSettings(
            Host: "chat.example",
            Username: "alice",
            Password: "secret",
            RememberLogin: true));

        Assert.Equal(ConnectionSettingsSaveStatus.SavedWithDegradedCredentialProtection, result.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
