using InternalsViewer.UI.Plugins.FullTrace.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Settings;

public sealed class InstructionHistorySettingsContributor(InstructionHistorySettings settings) : ITraceSettingsContributor
{
    public string Header => "Instruction History";

    public string Description => "Show Instructions on the menu of a call in the Trace Timeline";

    public FrameworkElement CreateContent()
    {
        var toggle = new ToggleSwitch { IsOn = settings.IsEnabled };

        toggle.Toggled += (_, _) => _ = settings.SetEnabledAsync(toggle.IsOn);

        return toggle;
    }
}
