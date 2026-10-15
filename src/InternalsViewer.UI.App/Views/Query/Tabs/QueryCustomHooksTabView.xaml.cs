using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
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

    private void OnHookMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        if (ViewModel?.CustomHooks is { } hooks)
        {
            var index = (int)e.Row.Id;

            e.Menu.Items.Add(MenuItems.Create("Remove", () => hooks.Remove(index)));
        }
    }
}
