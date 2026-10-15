using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class QueryCustomHooksTabView : UserControl
{
    public QueryCustomHooksTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private void OnCallClicked(object? sender, CallValue value) => ViewModel?.CustomHooks.ShowCall(value);
}
