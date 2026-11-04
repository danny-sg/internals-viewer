using InternalsViewer.Query.FullTrace.InstructionHistory.Documents;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Settings;
using InternalsViewer.UI.Plugins.FullTrace;
using InternalsViewer.UI.Plugins.FullTrace.Menus;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Menus;

public sealed class InstructionHistoryMenuContributor(IInstructionListingBuilder builder, InstructionHistorySettings settings)
    : ITraceMenuContributor
{
    private const string MenuText = "Instructions";

    private const string TitleSuffix = " Instructions";

    public void AddItems(List<TraceMenuItem> items, TraceMenuTarget target, ITracePluginHost host)
    {
        if (!settings.IsEnabled
            || target is not { Node: { Frame: { } frame } node, CallIndex: >= 0 and var callIndex }
            || host.Trace?.CallStore.GetCallList(frame.Address, frame.InstanceAddress) is not { } calls
            || callIndex >= calls.Count)
        {
            return;
        }

        var request = new InstructionRequest(frame,
                                             calls.GetThread(callIndex),
                                             calls.GetFromPosition(callIndex),
                                             calls.GetToPosition(callIndex));

        var key = $"Instructions:{request.Thread}:{request.From}";

        var title = node.Symbol + TitleSuffix;

        items.Add(new TraceMenuItem(MenuText, () => host.OpenDocument(key, () => CreateDocument(request, title))));
    }

    private InstructionListingDocument CreateDocument(InstructionRequest request, string title)
    {
        var model = new InstructionListingViewModel(builder, request, title);

        _ = model.LoadAsync();

        return new InstructionListingDocument(model);
    }
}
