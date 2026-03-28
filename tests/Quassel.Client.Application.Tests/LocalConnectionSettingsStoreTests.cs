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
            IsControlPanelOpen: true,
            LanguageCode: "nb");

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected.Host, actual.Host);
        Assert.Equal(expected.Port, actual.Port);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.Password, actual.Password);
        Assert.Equal(expected.TrustInvalidCertificates, actual.TrustInvalidCertificates);
        Assert.True(actual.RememberLogin);
        Assert.True(actual.IsControlPanelOpen);
        Assert.Equal(expected.LanguageCode, actual.LanguageCode);
    }

    [Fact]
    public void SaveAndLoad_ClearsCredentialsWhenRememberLoginIsDisabled()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = new LocalConnectionSettingsStore(path);

        store.Save(new StoredConnectionSettings(
            Host: "server.example",
            Port: 4242,
            Username: "bruker",
            Password: "passord",
            TrustInvalidCertificates: true,
            RememberLogin: false,
            IsControlPanelOpen: false,
            LanguageCode: "en_US"));

        var actual = store.Load();

        Assert.Equal("server.example", actual.Host);
        Assert.Equal(4242, actual.Port);
        Assert.True(actual.TrustInvalidCertificates);
        Assert.False(actual.RememberLogin);
        Assert.False(actual.IsControlPanelOpen);
        Assert.Equal("en_US", actual.LanguageCode);
        Assert.Equal(string.Empty, actual.Username);
        Assert.Equal(string.Empty, actual.Password);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
