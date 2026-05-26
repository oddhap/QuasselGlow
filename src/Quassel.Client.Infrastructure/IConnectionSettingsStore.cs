namespace Quassel.Client.Infrastructure;

public interface IConnectionSettingsStore
{
    ConnectionSettingsLoadResult Load();
    ConnectionSettingsSaveResult Save(StoredConnectionSettings settings);
}
