namespace InternalsViewer.UI.Plugins.Settings;

public interface IPluginSettingStore
{
    Task<T?> ReadAsync<T>(string key);

    Task SaveAsync<T>(string key, T value);
}
