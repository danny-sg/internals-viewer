using InternalsViewer.UI.App.Models.Query.Trace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceHashTablePairView : UserControl
{
    public TraceHashTablePairView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public TraceHashTablePair? ViewModel => DataContext as TraceHashTablePair;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is not { } pair)
        {
            return;
        }

        LocalPanel.DataContext = pair.Local;

        GlobalPanel.DataContext = pair.Global;
    }
}
