namespace Quassel.Client.Infrastructure;

public interface IConnectionSettingsStore
{
    StoredConnectionSettings Load();
    void Save(StoredConnectionSettings settings);
}
