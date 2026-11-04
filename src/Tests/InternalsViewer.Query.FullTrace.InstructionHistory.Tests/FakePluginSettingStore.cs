using InternalsViewer.UI.Plugins.Settings;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests;

internal sealed class FakePluginSettingStore : IPluginSettingStore
{
    public Dictionary<string, object?> Values { get; } = [];

    public Task<T?> ReadAsync<T>(string key) => Task.FromResult(Values.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SaveAsync<T>(string key, T value)
    {
        Values[key] = value;

        return Task.CompletedTask;
    }
}
