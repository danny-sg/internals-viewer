using System.Threading.Tasks;
using InternalsViewer.UI.Plugins.Settings;

namespace InternalsViewer.UI.App.Services.Plugins;

public sealed class PluginSettingStore(SettingsService settingsService) : IPluginSettingStore
{
    public Task<T?> ReadAsync<T>(string key) => settingsService.ReadSettingAsync<T>(key);

    public Task SaveAsync<T>(string key, T value) => settingsService.SaveSettingAsync(key, value);
}
