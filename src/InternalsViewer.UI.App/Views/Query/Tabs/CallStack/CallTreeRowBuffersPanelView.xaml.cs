using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallTreeRowBuffersPanelView : UserControl, IDocumentCommands
{
    private const double CommandSpacing = 4;

    private const string SourceToolTip = "Add a column after each field showing where its value came from: inline in the row buffer, "
                                         + "the buffer pool, a LOB, another operator's row buffer or batch, or other memory";

    public CallTreeRowBuffersPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public RowBuffersViewModel? ViewModel => DataContext as RowBuffersViewModel;

    public FrameworkElement CreateCommands()
        => TabCommands.Panel(CommandSpacing,
                             new DecodeToggle(),
                             TabCommands.Toggle(TabCommands.Label("Source"),
                                                ViewModel?.IsSourceVisible == true,
                                                SourceToolTip,
                                                (sender, _) => ViewModel?.SetSourceVisible(TabCommands.IsChecked(sender))));

    private void OnCallClicked(object? sender, CallValue value) => ViewModel?.Owner.Tree.ShowCall(value.Node, value.Call);

    private void OnCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        var call = (int)e.Row.Id;

        if (ViewModel is { } viewModel && viewModel.GetCallNode(call) is { } node)
        {
            e.Menu.Items.Add(MenuItems.Create("Call Stack", () => viewModel.Owner.ShowCallStack(node, call)));
        }
    }
}
