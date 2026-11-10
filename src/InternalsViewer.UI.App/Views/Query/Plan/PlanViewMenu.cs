using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Plan;

public static class PlanViewMenu
{
    public static MenuFlyout Create(QueryViewModel viewModel)
    {
        var menu = new MenuFlyout();

        if (viewModel.Layout.IsGuideMode)
        {
            menu.Items.Add(MenuItems.Toggle("Steps", viewModel.IsGuideStepsVisible, v => viewModel.IsGuideStepsVisible = v));

            menu.Items.Add(MenuItems.Toggle("Indicators", viewModel.IsGuideIndicatorVisible, v => viewModel.IsGuideIndicatorVisible = v));
        }

        if (viewModel.IsFullTrace)
        {
            menu.Items.Add(MenuItems.Toggle("Watches", viewModel.IsPlanWatchesVisible, v => viewModel.IsPlanWatchesVisible = v));
        }
        else
        {
            menu.Items.Add(MenuItems.Toggle("Annotations", viewModel.IsAnnotationsVisible, v => viewModel.IsAnnotationsVisible = v));
        }

        menu.Items.Add(MenuItems.Toggle("Properties", viewModel.IsPlanPropertiesVisible, v => viewModel.IsPlanPropertiesVisible = v));

        return menu;
    }
}
