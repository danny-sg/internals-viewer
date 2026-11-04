using InternalsViewer.UI.Plugins.Documents;
using Microsoft.UI.Xaml;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Documents;

public sealed class InstructionListingDocument(InstructionListingViewModel model) : IPluginDocument
{
    public string Title => Model.Title;

    private InstructionListingViewModel Model { get; } = model;

    private List<InstructionListingView> Views { get; } = [];

    public FrameworkElement CreateView()
    {
        var view = new InstructionListingView(Model);

        Views.Add(view);

        return view;
    }

    public void Dispose()
    {
        foreach (var view in Views)
        {
            view.Dispose();
        }

        Views.Clear();

        Model.Dispose();
    }
}
