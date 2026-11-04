using InternalsViewer.UI.Plugins.Settings;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Settings;

public sealed class InstructionHistorySettings
{
    private const string IsEnabledKey = "InstructionHistoryIsEnabled";

    public InstructionHistorySettings(IPluginSettingStore store)
    {
        Store = store;

        _ = LoadAsync();
    }

    public bool IsEnabled { get; private set; } = true;

    private IPluginSettingStore Store { get; }

    public Task SetEnabledAsync(bool isEnabled)
    {
        IsEnabled = isEnabled;

        return Store.SaveAsync(IsEnabledKey, isEnabled);
    }

    private async Task LoadAsync() => IsEnabled = await Store.ReadAsync<bool?>(IsEnabledKey) ?? true;
}
