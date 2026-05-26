namespace Quassel.Client.Infrastructure;

public enum ConnectionSettingsLoadStatus
{
    Loaded,
    Missing,
    Failed
}

public sealed record ConnectionSettingsLoadResult(
    StoredConnectionSettings Settings,
    ConnectionSettingsLoadStatus Status,
    string? Detail = null)
{
    public static ConnectionSettingsLoadResult Loaded(StoredConnectionSettings settings)
    {
        return new ConnectionSettingsLoadResult(settings, ConnectionSettingsLoadStatus.Loaded);
    }

    public static ConnectionSettingsLoadResult Missing()
    {
        return new ConnectionSettingsLoadResult(new StoredConnectionSettings(), ConnectionSettingsLoadStatus.Missing);
    }

    public static ConnectionSettingsLoadResult Failed(string? detail = null)
    {
        return new ConnectionSettingsLoadResult(new StoredConnectionSettings(), ConnectionSettingsLoadStatus.Failed, detail);
    }
}

public enum ConnectionSettingsSaveStatus
{
    Saved,
    SavedWithDegradedCredentialProtection,
    Failed
}

public sealed record ConnectionSettingsSaveResult(
    ConnectionSettingsSaveStatus Status,
    string? Detail = null)
{
    public static ConnectionSettingsSaveResult Saved()
    {
        return new ConnectionSettingsSaveResult(ConnectionSettingsSaveStatus.Saved);
    }

    public static ConnectionSettingsSaveResult SavedWithDegradedCredentialProtection()
    {
        return new ConnectionSettingsSaveResult(ConnectionSettingsSaveStatus.SavedWithDegradedCredentialProtection);
    }

    public static ConnectionSettingsSaveResult Failed(string? detail = null)
    {
        return new ConnectionSettingsSaveResult(ConnectionSettingsSaveStatus.Failed, detail);
    }
}
