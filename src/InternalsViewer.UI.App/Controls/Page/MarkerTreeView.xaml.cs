using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;


namespace InternalsViewer.UI.App.Controls.Page;
public sealed partial class MarkerTreeView : UserControl
{
    public ObservableCollection<Marker>? Markers
    {
        get { return ((ObservableCollection<Marker>)GetValue(MarkersProperty)).Where(m => m.IsVisible).ToObservableCollection(); }
        set { SetValue(MarkersProperty, value); }
    }

    public static readonly DependencyProperty MarkersProperty = DependencyProperty
        .Register(nameof(Markers),
            typeof(ObservableCollection<Marker>),
            typeof(MarkerTreeView),
            null);

    public Marker? SelectedMarker
    {
        get => (Marker?)GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    public static readonly DependencyProperty SelectedMarkerProperty
        = DependencyProperty.Register(nameof(SelectedMarker),
            typeof(Marker),
            typeof(MarkerTreeView),
            null);

    public MarkerTreeView()
    {
        this.InitializeComponent();

        VisualStateManager.GoToState(TreeView, "TreeViewMultiSelectEnabledUnselected", true);

    }
}
