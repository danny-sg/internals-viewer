using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Trace.CallStack;

public sealed partial class QueryCallTreeTabView : UserControl
{
    public QueryCallTreeTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();

        AddHandler(PointerPressedEvent, new PointerEventHandler(OnNavigationPointerPressed), handledEventsToo: true);
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private void OnNavigationPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.CallTreeTab.Tree is not { } callTree)
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsXButton1Pressed)
        {
            callTree.GoBack();

            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            callTree.GoForward();

            e.Handled = true;
        }
    }
}
