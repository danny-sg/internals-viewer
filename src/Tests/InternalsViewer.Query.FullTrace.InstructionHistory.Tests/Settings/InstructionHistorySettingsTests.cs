using InternalsViewer.Query.FullTrace.InstructionHistory.Settings;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Settings;

[Trait("Category", "Unit")]
public class InstructionHistorySettingsTests
{
    private const string IsEnabledKey = "InstructionHistoryIsEnabled";

    [Fact]
    public void Instruction_History_Is_Enabled_When_Nothing_Is_Saved()
        => Assert.True(new InstructionHistorySettings(new FakePluginSettingStore()).IsEnabled);

    [Fact]
    public void A_Saved_Setting_Is_Loaded()
    {
        var store = new FakePluginSettingStore();

        store.Values[IsEnabledKey] = (bool?)false;

        Assert.False(new InstructionHistorySettings(store).IsEnabled);
    }

    [Fact]
    public async Task Turning_Instruction_History_Off_Saves_It()
    {
        var store = new FakePluginSettingStore();

        var settings = new InstructionHistorySettings(store);

        await settings.SetEnabledAsync(false);

        Assert.False(settings.IsEnabled);
        Assert.Equal(false, store.Values[IsEnabledKey]);
    }
}
