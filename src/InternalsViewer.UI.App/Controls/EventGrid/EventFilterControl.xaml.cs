using System.Collections.ObjectModel;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.QueryReplay;

public sealed partial class EventFilterControl : UserControl
{
    public ObservableCollection<EventFilterNode> FilterNodes
    {
        get => (ObservableCollection<EventFilterNode>)GetValue(FilterNodesProperty);
        set => SetValue(FilterNodesProperty, value);
    }

    public static readonly DependencyProperty FilterNodesProperty =
        DependencyProperty.Register(nameof(FilterNodes), typeof(ObservableCollection<EventFilterNode>), typeof(EventFilterControl),
            new PropertyMetadata(new ObservableCollection<EventFilterNode>()));

    public bool IncludeSystemObjects
    {
        get => (bool)GetValue(IncludeSystemObjectsProperty);
        set => SetValue(IncludeSystemObjectsProperty, value);
    }

    public static readonly DependencyProperty IncludeSystemObjectsProperty =
        DependencyProperty.Register(nameof(IncludeSystemObjects), typeof(bool), typeof(EventFilterControl),
            new PropertyMetadata(false));

    public EventFilterControl()
    {
        InitializeComponent();
    }
}
