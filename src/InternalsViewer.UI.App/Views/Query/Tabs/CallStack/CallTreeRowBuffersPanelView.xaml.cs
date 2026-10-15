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
    public CallTreeRowBuffersPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public RowBuffersViewModel? ViewModel => DataContext as RowBuffersViewModel;

    public FrameworkElement CreateCommands() => TabCommands.Panel(0, new DecodeToggle());

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
